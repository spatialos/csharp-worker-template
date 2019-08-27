using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Improbable.Stdlib.Platform
{
    public readonly struct LoadBalancing
    {
        [JsonProperty("layer_configurations")]
        public readonly ImmutableArray<LayerConfiguration> LayerConfigurations;
    }
}
