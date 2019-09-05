using Newtonsoft.Json;

namespace Improbable.Stdlib.Project
{
    /// <summary>
    /// This is an incomplete API. Fields will be added as needed.
    /// </summary>
    public readonly struct LayerConfiguration
    {
        [JsonProperty("layer")]
        public readonly string Layer;

        [JsonProperty("options")]
        public readonly LayerOptions Options;
    }
}
