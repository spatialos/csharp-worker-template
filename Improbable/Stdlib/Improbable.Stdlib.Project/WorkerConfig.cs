using Newtonsoft.Json;

namespace Improbable.Stdlib.Project
{
    /// <summary>
    /// This is an incomplete API. Fields will be added as needed.
    /// </summary>
    public readonly struct WorkerConfig
    {
        [JsonProperty("workerType")]
        public readonly string WorkerType;

        [JsonProperty("layer")]
        public readonly string Layer;
    }
}
