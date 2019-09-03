using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Improbable.Stdlib.Platform
{
    /// <summary>
    /// This is an incomplete API. Fields will be added as needed.
    /// </summary>
    public readonly struct LaunchConfig
    {
        [JsonProperty("workerFlags")]
        public readonly ImmutableArray<DeploymentWorkerFlags> WorkerFlags;

        [JsonProperty("loadBalancing")]
        public readonly LoadBalancing LoadBalancing;
    }
}
