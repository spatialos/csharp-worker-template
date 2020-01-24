using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Improbable.Schema.Bundle
{
    [DebuggerDisplay("{" + nameof(QualifiedName) + "}")]
    public readonly struct TypeDescription
    {
        public readonly string QualifiedName;

        public readonly string OuterType;

        public readonly SourceReference SourceReference;

        public readonly SchemaFile SourceFile;

        public readonly IReadOnlyList<TypeDescription> NestedTypes;

        public readonly IReadOnlyList<EnumDefinition> NestedEnums;

        public readonly IReadOnlyList<FieldDefinition> Fields;

        public readonly IReadOnlyList<Annotation> Annotations;

        public readonly IReadOnlyList<ComponentDefinition.EventDefinition> Events;

        public readonly IReadOnlyList<string> Warnings;

        public readonly uint? ComponentId;

        /// <summary>
        ///     If true, this is a restricted Improbable component, which workers can never gain authority over.
        ///     Code generators can use this to avoid generating code that may be nonsensical or confusing.
        /// </summary>
        public readonly bool IsRestricted;

        public TypeDescription(string qualifiedName, Bundle bundle)
        {
            QualifiedName = qualifiedName;

            IsRestricted = qualifiedName.StartsWith("improbable.restricted");

            NestedEnums = bundle.Enums.Where(e => e.Value.OuterType == qualifiedName)
                .Select(type => bundle.Enums[type.Key]).ToList();

            bundle.Components.TryGetValue(qualifiedName, out var component);
            ComponentId = component?.ComponentId;

            SourceFile = bundle.TypeToFile[qualifiedName];

            if (ComponentId.HasValue)
            {
                SourceReference = bundle.Components[qualifiedName].SourceReference;
                OuterType = string.Empty;
            }
            else
            {
                SourceReference = bundle.Types[qualifiedName].SourceReference;
                OuterType = bundle.Types[qualifiedName].OuterType;
            }

            NestedTypes = bundle.Types.Where(t => t.Value.OuterType == qualifiedName)
                .Select(type => new TypeDescription(bundle.Types[type.Key].QualifiedName, bundle)).ToList();

            Fields = component?.Fields;

            if (!string.IsNullOrEmpty(component?.DataDefinition))
            {
                // Inline fields into the component.
                Fields = bundle.Types[component.DataDefinition].Fields;
            }

            if (Fields == null)
            {
                Fields = ComponentId.HasValue ? bundle.Components[qualifiedName].Fields : bundle.Types[qualifiedName].Fields;
            }

            if (Fields == null)
            {
                throw new Exception("Internal error: no fields found");
            }

            var warnings = new List<string>();
            Warnings = warnings;

            Fields = Fields.Where(f =>
            {
                var allowed = !IsPrimitiveEntityField(f);

                if (!allowed)
                {
                    warnings.Add($"field '{qualifiedName}.{f.Name}' is the Entity type, which is currently unsupported.");
                }

                var allowed2 = !IsFieldTypeRecursive(bundle, qualifiedName, f);
                if (!allowed2)
                {
                    warnings.Add($"field '{qualifiedName}.{f.Name}' recursively references {qualifiedName}, which is currently unsupported.");
                }

                return allowed && allowed2;
            }).ToList();

            Annotations = component != null ? component.Annotations : bundle.Types[qualifiedName].Annotations;
            Events = component?.Events;
        }

        private static bool IsPrimitiveEntityField(FieldDefinition f)
        {
            // The Entity primitive type is currently unsupported, and undocumented.
            // It is ignored for now.
            return f.TypeSelector switch
            {
                FieldType.Map => (f.MapType.KeyType.HasPrimitive(PrimitiveType.Entity) || f.MapType.ValueType.HasPrimitive(PrimitiveType.Entity)),
                _ => f.HasPrimitive(PrimitiveType.Entity)
            };
        }

        private static bool IsFieldTypeRecursive(Bundle bundle, string qualifiedRootTypeName, FieldDefinition field)
        {
            return field.IsOption() &&
                   (field.HasCustomType(qualifiedRootTypeName) || field.HasCustomType() && bundle.Types[field.OptionType.InnerType.Type].Fields.Any(f => IsFieldTypeRecursive(bundle, qualifiedRootTypeName, f)));
        }
    }
}
