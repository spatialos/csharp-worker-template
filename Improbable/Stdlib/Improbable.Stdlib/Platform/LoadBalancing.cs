using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Improbable.Stdlib.Platform
{
    /// <summary>
    /// This is an incomplete API. Fields will be added as needed.
    /// </summary>
    public readonly struct LoadBalancing
    {
        [JsonProperty("layer_configurations")]
        public readonly ImmutableArray<LayerConfiguration> LayerConfigurations;
    }
}
