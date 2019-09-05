using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Improbable.Stdlib.Project
{
    /// <summary>
    /// This is an incomplete API. Fields will be added as needed.
    /// </summary>
    public readonly struct ProjectConfig
    {
        [JsonProperty("configurationVersion")]
        public readonly string ConfigurationVersion;

        [JsonProperty("projectName")]
        public readonly string ProjectName;

        [JsonProperty("schemaDescriptor")]
        public readonly string SchemaDescriptor;

        [JsonProperty("clientWorkers")]
        public readonly ImmutableArray<string> ClientWorkers;

        [JsonProperty("serverWorkers")]
        public readonly ImmutableArray<string> ServerWorkers;
    }
}
