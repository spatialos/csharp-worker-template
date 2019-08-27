using Newtonsoft.Json;

namespace Improbable.Stdlib.Platform
{
    public readonly struct LayerOptions
    {
        [JsonProperty("manual_worker_connection_only")]
        public readonly bool ManualWorkerConnectionOnly;
    }
}
