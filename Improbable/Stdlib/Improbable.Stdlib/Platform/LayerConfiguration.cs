using Newtonsoft.Json;

namespace Improbable.Stdlib.Platform
{
    public readonly struct LayerConfiguration
    {
        [JsonProperty("layer")]
        public readonly string Layer;

        [JsonProperty("options")]
        public readonly LayerOptions Options;
    }
}
