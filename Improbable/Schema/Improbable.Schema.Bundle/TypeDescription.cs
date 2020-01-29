using System.Collections.Generic;
using System.Collections.Immutable;
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

        public readonly ImmutableList<TypeDescription> NestedTypes;

        public readonly ImmutableArray<EnumDefinition> NestedEnums;

        public readonly ImmutableArray<FieldDefinition> Fields;

        public readonly ImmutableArray<Annotation> Annotations;

        public readonly ImmutableArray<ComponentDefinition.EventDefinition> Events;

        public readonly ImmutableArray<string> Warnings;

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

            NestedEnums = ImmutableArray.CreateRange(bundle.Enums.Where(e => e.Value.OuterType == qualifiedName).Select(type => bundle.Enums[type.Key]));

            if (bundle.Components.TryGetValue(qualifiedName, out var component))
            {
                ComponentId = component.ComponentId;

                SourceReference = bundle.Components[qualifiedName].SourceReference;
                OuterType = string.Empty;

                Events = component.Events;
                Annotations = component.Annotations;

                Fields = !string.IsNullOrEmpty(component.DataDefinition) ? bundle.Types[component.DataDefinition].Fields : bundle.Components[qualifiedName].Fields;
            }
            else
            {
                SourceReference = bundle.Types[qualifiedName].SourceReference;
                OuterType = bundle.Types[qualifiedName].OuterType;

                Fields = bundle.Types[qualifiedName].Fields;
                Annotations = bundle.Types[qualifiedName].Annotations;
                ComponentId = null;
            }

            SourceFile = bundle.TypeToFile[qualifiedName];

            NestedTypes = ImmutableList.CreateRange(bundle.Types.Where(t => t.Value.OuterType == qualifiedName).Select(type =>
            {
                var t = bundle.Types[type.Key];
                return new TypeDescription(t.QualifiedName, bundle);
            }));

            var warnings = ImmutableArray<string>.Empty;
            Fields = ImmutableArray.CreateRange(Fields.Where(f =>
            {
                var isEntityField = IsPrimitiveEntityField(f);

                if (isEntityField)
                {
                    warnings = warnings.Add($"field '{qualifiedName}.{f.Name}' is the Entity type, which is currently unsupported.");
                }

                var isRecursiveField = IsFieldTypeRecursive(bundle, qualifiedName, f);
                if (isRecursiveField)
                {
                    warnings = warnings.Add($"field '{qualifiedName}.{f.Name}' recursively references {qualifiedName}, which is currently unsupported.");
                }

                return !isEntityField && !isRecursiveField;
        }));

            Warnings = warnings;
        }

        private static bool IsPrimitiveEntityField(FieldDefinition f)
        {
#nullable disable
            // The Entity primitive type is currently unsupported, and undocumented.
            // It is ignored for now.
            return f.TypeSelector switch
            {
                FieldType.Map => (f.MapType.KeyType.HasPrimitive(PrimitiveType.Entity) || f.MapType.ValueType.HasPrimitive(PrimitiveType.Entity)),
                _ => f.HasPrimitive(PrimitiveType.Entity)
            };
#nullable restore
        }

        private static bool IsFieldTypeRecursive(Bundle bundle, string qualifiedRootTypeName, FieldDefinition field)
        {
#nullable disable
            return field.IsOption() &&
                   (field.HasCustomType(qualifiedRootTypeName) || field.HasCustomType() && bundle.Types[field.OptionType.InnerType.Type].Fields.Any(f => IsFieldTypeRecursive(bundle, qualifiedRootTypeName, f)));
#nullable restore
        }
    }
}
