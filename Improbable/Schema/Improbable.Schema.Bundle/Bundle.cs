using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Improbable.Schema.Bundle
{
    public readonly struct Bundle
    {
        public SchemaBundle SchemaBundle { get; }
        public ImmutableDictionary<string, TypeDefinition> Types { get; }
        public ImmutableDictionary<string, EnumDefinition> Enums { get; }
        public ImmutableDictionary<string, ComponentDefinition> Components { get; }
        public ImmutableDictionary<string, SchemaFile> TypeToFile { get; }
        public ImmutableArray<string> CommandTypes { get; }

        public Bundle(SchemaBundle bundle)
        {
            SchemaBundle = bundle;

            Components = ImmutableDictionary.CreateRange(bundle.SchemaFiles.SelectMany(f => f.Components).ToDictionary(c => c.QualifiedName, c => c));
            Types = ImmutableDictionary.CreateRange(bundle.SchemaFiles.SelectMany(f => f.Types).ToDictionary(t => t.QualifiedName, t => t));
            Enums = ImmutableDictionary.CreateRange(bundle.SchemaFiles.SelectMany(f => f.Enums).ToDictionary(t => t.QualifiedName, t => t));
            CommandTypes = ImmutableArray.CreateRange(bundle.SchemaFiles.SelectMany(f => f.Components)
                .SelectMany(c => c.Commands)
                .SelectMany(cmd => new[] { cmd.RequestType, cmd.ResponseType }).ToList());

            var fileNameDict = new Dictionary<string, SchemaFile>();

            foreach (var file in bundle.SchemaFiles)
            {
                foreach (var type in file.Types)
                {
                    fileNameDict[type.QualifiedName] = file;
                }

                foreach (var type in file.Components)
                {
                    fileNameDict[type.QualifiedName] = file;
                }

                foreach (var type in file.Enums)
                {
                    fileNameDict[type.QualifiedName] = file;
                }
            }

            TypeToFile = ImmutableDictionary.CreateRange(fileNameDict);
        }
    }
}
