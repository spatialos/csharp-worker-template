using Newtonsoft.Json;

namespace Improbable.Stdlib.Project
{
    /// <summary>
    /// This is an incomplete API. Fields will be added as needed.
    /// </summary>
    public readonly struct LayerOptions
    {
        [JsonProperty("manual_worker_connection_only")]
        public readonly bool ManualWorkerConnectionOnly;
    }
}
