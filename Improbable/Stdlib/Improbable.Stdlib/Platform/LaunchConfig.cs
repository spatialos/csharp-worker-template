using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Improbable.Stdlib.Platform
{
    public readonly struct LaunchConfig
    {
        [JsonProperty("workerFlags")]
        public readonly ImmutableArray<DeploymentWorkerFlags> WorkerFlags;

        [JsonProperty("loadBalancing")]
        public readonly LoadBalancing LoadBalancing;
    }
}
