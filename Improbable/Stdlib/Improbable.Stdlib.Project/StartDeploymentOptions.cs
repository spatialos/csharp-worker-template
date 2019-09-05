using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Improbable.Stdlib.Project
{
    public readonly struct StartDeploymentOptions
    {
        public readonly string LaunchConfigPath;

        public readonly string ProjectConfigPath;

        public readonly string SnapshotId;

        public readonly ImmutableArray<string> Tags;

        public readonly ImmutableArray<DeploymentWorkerFlags> WorkerFlags;

        public readonly ProjectConfig ProjectConfig;

        public readonly LaunchConfig LaunchConfig;

        public StartDeploymentOptions(string launchConfigPath, string projectConfigPath, string snapshotId, IEnumerable<string> tags = null,
            IEnumerable<DeploymentWorkerFlags> workerFlags = null)
        {
            LaunchConfigPath = launchConfigPath ?? throw new ArgumentNullException(nameof(launchConfigPath));
            ProjectConfigPath = projectConfigPath ?? throw new ArgumentNullException(nameof(projectConfigPath));
            SnapshotId = snapshotId;

            Tags = ImmutableArray<string>.Empty;
            if (tags != null)
            {
                Tags = Tags.AddRange(tags);
            }

            WorkerFlags = ImmutableArray<DeploymentWorkerFlags>.Empty;
            if (workerFlags != null)
            {
                WorkerFlags = WorkerFlags.AddRange(workerFlags);
            }

            ProjectConfig = JsonConvert.DeserializeObject<ProjectConfig>(File.ReadAllText(projectConfigPath, Encoding.UTF8));
            LaunchConfig = JsonConvert.DeserializeObject<LaunchConfig>(File.ReadAllText(launchConfigPath, Encoding.UTF8));
        }
    }
}
