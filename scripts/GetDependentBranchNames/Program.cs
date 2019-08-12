using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace BuildNugetPackages
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                var currentBranch = RunRedirected("git", "rev-parse", "--abbrev-ref", "HEAD").Trim();
                var checkRemoteBranch = RunRedirected("git", "ls-remote", "--heads", "https://github.com/spatialos/database-sync-worker.git", currentBranch).Trim();

                var remoteBranch = currentBranch == checkRemoteBranch ? currentBranch : "master";

                Console.Out.WriteLine($@"steps:
  - label: ""build-database-sync-worker""
    trigger: database-sync-worker-premerge
    build:
      branch: {remoteBranch}
      env:
        CSHARP_TEMPLATE_BRANCH: ""{currentBranch}""");

            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }
            return 0;
        }

        private static string RunRedirected(string command, params string[] args)
        {
            var info = new ProcessStartInfo(command, string.Join(' ', args))
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using (var process = Process.Start(info))
            {
                if (process == null) throw new Exception($"Failed to start {command}");

                var output = new StringBuilder();
                process.OutputDataReceived += (sender, eventArgs) =>
                {
                    if (!string.IsNullOrEmpty(eventArgs.Data))
                    {
                        output.AppendLine(eventArgs.Data);
                    }
                };

                process.ErrorDataReceived += (sender, eventArgs) =>
                {
                    if (!string.IsNullOrEmpty(eventArgs.Data))
                    {
                        Console.Error.WriteLine(eventArgs.Data);
                    }
                };

                process.EnableRaisingEvents = true;
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Non-zero exit code: {command} {string.Join(" ", args)}");
                }

                return output.ToString();
            }
        }
    }
}
