using System;
using System.Collections.Generic;
using System.Linq;
using Medallion.Shell;

namespace BuildNugetPackages
{
    internal class Program
    {
        private static readonly Shell shell = new Shell(options => options.ThrowOnError());

        private static int Main(string[] args)
        {
            try
            {
                // Default to "master", unless the downstream dependency has a branch matching the same name, then use that.
                var currentBranch = Environment.GetEnvironmentVariable("BUILDKITE_BRANCH") ?? "master";

                var lines = new List<string>();
                shell.Run("git", "ls-remote", "--heads", "https://github.com/spatialos/database-sync-worker.git", currentBranch).RedirectTo(lines).Wait();

                var remoteBranch = lines.Any() && lines.First().Contains(currentBranch) ? currentBranch : "master";

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
    }
}
