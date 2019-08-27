using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Google.Api.Gax.Grpc;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Improbable.SpatialOS.Deployment.V1Alpha1;
using Improbable.SpatialOS.Platform.Common;
using Improbable.SpatialOS.PlayerAuth.V2Alpha1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Improbable.Stdlib.Platform
{
    public static class Platform
    {
        public static string ToolbeltConfigDir =>
            Environment.GetEnvironmentVariable("IMPROBABLE_CONFIG_DIR") ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ".improbable")
                : Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".improbable"));

        private static PlatformCredential GetCredentials()
        {
            var refreshToken = File.ReadAllText(Path.Combine(ToolbeltConfigDir, "oauth2/oauth2_refresh_token"));
            return new PlatformRefreshTokenCredential(refreshToken);
        }

        private static PlatformApiChannel GetLocalApiChannel()
        {
            return new PlatformApiChannel(GetCredentials(), new PlatformApiEndpoint("localhost", 9876, true));
        }

        public static Deployment StartLocal(in StartDeploymentOptions startDeployment)
        {
            var client = DeploymentServiceClient.Create(GetLocalApiChannel());

            Shell.Run("spatial", "auth", "login", "--json_output");

            // The runtime creates a '.improbable' folder next to the spatialos.json file.
            var rootDir = Path.GetDirectoryName(startDeployment.ProjectConfigPath);
            var tempDir = Path.Combine(rootDir, ".improbable");

            Directory.CreateDirectory(tempDir);

            var splConfigPath = Path.Combine(tempDir, "spl_config.json");
            Shell.Run(
                "spatial", "alpha", "config", "convert", "--json_output", "--launch_config", Shell.Escape(startDeployment.LaunchConfigPath),
                "--main_config", Shell.Escape(startDeployment.ProjectConfigPath), "--output_path", splConfigPath);

            var project = JsonConvert.DeserializeObject<ProjectConfig>(File.ReadAllText(startDeployment.ProjectConfigPath, Encoding.UTF8));

            var needsStart = true;
            var pidFile = Path.Combine(ToolbeltConfigDir, "spatiald.pid");
            var lockFile = Path.Combine(ToolbeltConfigDir, "spatiald.lock");
            if (File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile).Trim(), out var pid))
            {
                try
                {
                    Process.GetProcessById(pid);
                    needsStart = false;
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
                Shell.Run("spatial", "service", "start", "--json_output", "--main_config", Shell.Escape(startDeployment.ProjectConfigPath));
            }

            using (var tcs = new CancellationTokenSource(5000))
            {
                var timeout = CallSettings.FromCancellationToken(tcs.Token);
                var deployments = client.ListDeployments(new ListDeploymentsRequest
                    { DeploymentName = "local", ProjectName = project.ProjectName }, timeout);

                foreach (var dpl in deployments.Where(dpl => dpl.Status == Deployment.Types.Status.Running))
                {
                    client.DeleteDeployment(new DeleteDeploymentRequest { Id = dpl.Id });
                }
            }

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
            }).PollUntilCompleted().Result;

            // Apply desired starting worker flags.
            if (startDeployment.WorkerFlags.Any())
            {
                var allWorkerFlags = new RepeatedField<WorkerFlag>();

                var startFlags = startDeployment.WorkerFlags.SelectMany(wf =>
                    wf.Flags.Select(f => new WorkerFlag { Key = f.Name, Value = f.Value, WorkerType = wf.WorkerType }));

                allWorkerFlags.Add(startFlags);

                var updatedFields = new Deployment{ Name= response.Name, ProjectName = project.ProjectName, Id = response.Id, WorkerFlags = { allWorkerFlags } };
                client.UpdateDeployment(new UpdateDeploymentRequest { Deployment = updatedFields, UpdateMask = new FieldMask {Paths = { "worker_flags"}}});
            }

            return response;
        }

        public static void Stop(in ProjectConfig config)
        {
            using (var tcs = new CancellationTokenSource(5000))
            {
                var client = DeploymentServiceClient.Create(GetLocalApiChannel());

                var timeout = CallSettings.FromCancellationToken(tcs.Token);
                var deployments = client.ListDeployments(new ListDeploymentsRequest
                    { DeploymentName = "local", ProjectName = config.ProjectName }, timeout);

                foreach (var dpl in deployments.Where(dpl =>
                    dpl.Status == Deployment.Types.Status.Running))
                {
                    client.DeleteDeployment(new DeleteDeploymentRequest { Id = dpl.Id });
                }
            }
        }

        public static string CreateDevAuthToken(in ProjectConfig config)
        {
            using (var tcs = new CancellationTokenSource(5000))
            {
                var timeout = CallSettings.FromCancellationToken(tcs.Token);
                var authClient = PlayerAuthServiceClient.Create(credentials: GetCredentials());

                var token = authClient.CreateDevelopmentAuthenticationToken(new CreateDevelopmentAuthenticationTokenRequest
                {
                    Description = config.ProjectName,
                    Lifetime = Duration.FromTimeSpan(TimeSpan.FromMinutes(30)),
                    ProjectName = config.ProjectName
                }, timeout);

                return token.TokenSecret;
            }
        }

        // Hide this away until we don't need to call out to spatial any more.
       private static class Shell
        {
            public static void Run(string command, params string[] args)
            {
                var processStartInfo = new ProcessStartInfo(command, string.Join(" ", args))
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                
                var process = Process.Start(processStartInfo);
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();

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
                            stdout.AppendLine(msgValue.Value<string>());
                        }
                        else
                        {
                            stdout.AppendLine(eventArgs.Data);
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
                            stderr.AppendLine(msgValue.Value<string>());
                        }
                        else
                        {
                            stderr.AppendLine(eventArgs.Data);
                        }
                    }
                };

                process.EnableRaisingEvents = true;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"ExitCode {process.ExitCode}: {processStartInfo.FileName} {processStartInfo.Arguments}\n{stdout}\n{stderr}");
                }
            }

            public static string Escape(string arg)
            {
                return arg.Any(char.IsSeparator) ? $"\"{arg}\"" : arg;
            }
        }
    }
}
