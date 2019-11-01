using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Api.Gax;
using Google.Api.Gax.Grpc;
using Google.Protobuf.WellKnownTypes;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using Improbable.Stdlib.Project;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Improbable.Stdlib.Platform
{
    /// <summary>
    /// This is an incomplete API. Functionality will be added as needed.
    /// </summary>
    public static class Platform
    {
        private static string ImprobableConfigDir =>
            Environment.GetEnvironmentVariable("IMPROBABLE_CONFIG_DIR") ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".improbable")
                : Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".improbable"));

        private static readonly PollSettings DefaultPolling = new PollSettings(Expiration.FromTimeout(TimeSpan.FromMinutes(2)), TimeSpan.FromMilliseconds(100));

        private static PlatformCredential GetCredentials()
        {
            var refreshToken = File.ReadAllText(Path.Combine(ImprobableConfigDir, "oauth2", "oauth2_refresh_token"));
            return new PlatformRefreshTokenCredential(refreshToken);
        }

        private static PlatformApiChannel GetApiChannel(in NetworkEnvironment env)
        {
            return new PlatformApiChannel(GetCredentials(), new PlatformApiEndpoint(env.ServiceIp, env.ServicePort, env.Secure == false));
        }

        public static async Task<Deployment> StartLocalAsync(StartDeploymentOptions startDeployment, NetworkEnvironment env, CancellationToken cancellation = default, IProgress<string> progress = null)
        {
            var client = DeploymentServiceClient.Create(GetApiChannel(env));

            progress?.Report("Logging in...");
            Shell.Run("spatial", progress, "auth", "login", "--json_output");

            // The runtime creates a '.improbable' folder next to the spatialos.json file.
            var rootDir = Path.GetDirectoryName(startDeployment.ProjectConfigPath);
            var tempDir = Path.Combine(rootDir, ".improbable");

            Directory.CreateDirectory(tempDir);

            progress?.Report("Converting launch config...");
            var splConfigPath = Path.Combine(tempDir, "spl_config.json");
            Shell.Run(
                "spatial", progress, "alpha", "config", "convert", "--json_output", "--launch_config", Shell.Escape(startDeployment.LaunchConfigPath),
                "--main_config", Shell.Escape(startDeployment.ProjectConfigPath), "--output_path", splConfigPath);

            var project = JsonConvert.DeserializeObject<ProjectConfig>(File.ReadAllText(startDeployment.ProjectConfigPath, Encoding.UTF8));

            var needsStart = true;
            var pidFile = Path.Combine(ImprobableConfigDir, "spatiald.pid");
            var lockFile = Path.Combine(ImprobableConfigDir, "spatiald.lock");
            if (File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid))
            {
                try
                {
                    var process = Process.GetProcessById(pid);
                    if (process.MainModule != null && process.MainModule.FileName.Contains("spatiald"))
                    {
                        needsStart = false;
                    }
                }
                catch
                {
                    File.Delete(pidFile);

                    if (File.Exists(lockFile))
                    {
                        File.Delete(lockFile);
                    }
                }
            }

            if (needsStart)
            {
                progress?.Report("Starting Spatial service...");
                Shell.Run("spatial", progress, "service", "start", "--json_output", "--main_config", Shell.Escape(startDeployment.ProjectConfigPath), "--runtime_ip", env.RuntimeIp);
            }

            // Shut down any currently-running local deployments...
            await StopLocalAsync(startDeployment.ProjectConfig, env, cancellation, progress);

            var snapshotFile = Path.Combine(Path.GetDirectoryName(startDeployment.ProjectConfigPath) ?? "", "snapshots", $"{startDeployment.SnapshotId}.snapshot");
            if (!File.Exists(snapshotFile))
            {
                throw new FileNotFoundException(snapshotFile);
            }

            progress?.Report($"Starting local deployment for '{project.ProjectName}' with snapshot '{startDeployment.SnapshotId}', runtimeIP: {env.RuntimeIp}, serviceIP: {env.ServiceIp}...");
            var response = client.CreateDeployment(new CreateDeploymentRequest
                {
                    Deployment = new Deployment
                    {
                        Tag = { startDeployment.Tags },
                        Name = "local",
                        StartingSnapshotId = startDeployment.SnapshotId,
                        ProjectName = project.ProjectName,
                        LaunchConfig = new SpatialOS.Deployment.V1Alpha1.LaunchConfig
                        {
                            ConfigJson = File.ReadAllText(splConfigPath)
                        }
                    }
                }, CallSettings.FromCancellationToken(cancellation))
                .PollUntilCompleted(DefaultPolling);

            // Apply desired starting worker flags.
            // This is applied separately to ensure that the static, config-based worker flags aren't removed.
            if (startDeployment.WorkerFlags.Any())
            {
                var updatedFields = new Deployment
                {
                    Name = response.Result.Name,
                    ProjectName = project.ProjectName,
                    Id = response.Result.Id,
                    WorkerFlags = { response.Result.WorkerFlags.Union(startDeployment.WorkerFlags.AsPlatformFlags()) }
                };

                progress?.Report("Applying worker flags...");
                await client.UpdateDeploymentAsync(new UpdateDeploymentRequest
                {
                    Deployment = updatedFields,
                    UpdateMask = new FieldMask { Paths = { "worker_flags" } }
                }, cancellation);
            }

            return response.Result;
        }

        public static async Task StopLocalAsync(ProjectConfig config, NetworkEnvironment env,  CancellationToken cancellation = default, IProgress<string> progress = null)
        {
            var client = DeploymentServiceClient.Create(GetApiChannel(env));

            var deployments = await client.ListDeploymentsAsync(new ListDeploymentsRequest
            {
                DeploymentName = "local",
                ProjectName = config.ProjectName,
                DeploymentStoppedStatusFilter = ListDeploymentsRequest.Types.DeploymentStoppedStatusFilter.NotStoppedDeployments
            }, CallSettings.FromCancellationToken(cancellation)).ReadPageAsync(50, cancellation);

            var tasks = new List<Task>();
            foreach (var dpl in deployments)
            {
                progress.Report($"Stopping {dpl.Id}...");
                tasks.Add(client.DeleteDeploymentAsync(new DeleteDeploymentRequest { Id = dpl.Id }, CallSettings.FromCancellationToken(cancellation)));
            }

            Task.WaitAll(tasks.ToArray(), cancellation);
        }


        public static async Task<string> CreateDevAuthTokenAsync(ProjectConfig config, CancellationToken cancellation = default)
        {
            var authClient = PlayerAuthServiceClient.Create(credentials: GetCredentials());

            var token = await authClient.CreateDevelopmentAuthenticationTokenAsync(new CreateDevelopmentAuthenticationTokenRequest
            {
                Description = config.ProjectName,
                Lifetime = Duration.FromTimeSpan(TimeSpan.FromMinutes(30)),
                ProjectName = config.ProjectName
            }, CallSettings.FromCancellationToken(cancellation));

            return token.TokenSecret;
        }

        // Hide this away until we don't need to call out to spatial any more.
        private static class Shell
        {
            public static void Run(string command, IProgress<string> progress, params string[] args)
            {
                var processStartInfo = new ProcessStartInfo(command, string.Join(" ", args))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                    {
                        throw new Exception($"Failed to start {command} {processStartInfo.Arguments}");
                    }

                    process.OutputDataReceived += (sender, eventArgs) =>
                    {
                        if (!string.IsNullOrEmpty(eventArgs.Data))
                        {
                            var obj = JObject.Parse(eventArgs.Data);

                            if (obj.TryGetValue("msg", out var msgValue))
                            {
                                progress?.Report(msgValue.Value<string>());
                            }
                            else
                            {
                                progress?.Report(eventArgs.Data);
                            }
                        }
                    };

                    process.ErrorDataReceived += (sender, eventArgs) =>
                    {
                        if (!string.IsNullOrEmpty(eventArgs.Data))
                        {
                            var obj = JObject.Parse(eventArgs.Data);

                            if (obj.TryGetValue("msg", out var msgValue))
                            {
                                progress?.Report(msgValue.Value<string>());
                            }
                            else
                            {
                                progress?.Report(eventArgs.Data);
                            }
                        }
                    };

                    process.EnableRaisingEvents = true;
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        throw new Exception($"ExitCode {process.ExitCode}: {processStartInfo.FileName} {processStartInfo.Arguments}");
                    }
                }
            }

            public static string Escape(string arg)
            {
                return arg.Any(char.IsSeparator) ? $"\"{arg}\"" : arg;
            }
        }
    }
}
