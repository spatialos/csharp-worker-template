using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Improbable.Schema.Bundle
{
    public static class SchemaBundleLoader
    {
        public static Bundle LoadBundle(string filename)
        {
            var contractResolver = new DefaultContractResolver
            {
                NamingStrategy = new PascalCaseNamingStrategy()
            };

            var settings = new JsonSerializerSettings
            {
                ContractResolver = contractResolver,
                MissingMemberHandling = MissingMemberHandling.Error
            };

            var bundleFile = JsonConvert.DeserializeObject<SchemaBundle>(File.ReadAllText(filename, Encoding.UTF8), settings);
            if (bundleFile == null)
            {
                throw new InvalidOperationException($"Failed to deserialize {filename}");
            }

            return new Bundle(bundleFile);
        }

        private class PascalCaseNamingStrategy : NamingStrategy
        {
            protected override string ResolvePropertyName(string name)
            {
                var pascal = char.ToLowerInvariant(name[0]) + name.Substring(1, name.Length - 1);
                return pascal;
            }
        }
    }
}
