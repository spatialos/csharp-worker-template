using Newtonsoft.Json;

namespace Improbable.Stdlib.Project
{
    public readonly struct Flag
    {
        [JsonProperty("name")]
        public readonly string Name;
        [JsonProperty("value")]
        public readonly string Value;

        public Flag(string name, string value)
        {
            Name = name;
            Value = value;
        }
    }
}
