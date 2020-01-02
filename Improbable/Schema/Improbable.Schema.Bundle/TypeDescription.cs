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
        /// If true, this is a restricted Improbable component, which workers can never gain authority over.
        /// Code generators can use this to avoid generating code that may be nonsensical or confusing.
        /// </summary>
        public readonly bool IsRestricted;

        public TypeDescription(string qualifiedName, Bundle bundle)
        {
            QualifiedName = qualifiedName;

            IsRestricted = qualifiedName.StartsWith("improbable.restricted");

            NestedEnums = bundle.Enums.Where(e => e.Value.OuterType == qualifiedName).Select(type => bundle.Enums[type.Key]).ToList();

            if (bundle.Components.TryGetValue(qualifiedName, out var component))
            {
                ComponentId = component.ComponentId;

                SourceReference = bundle.Components[qualifiedName].SourceReference;
                OuterType = "";

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
                Events = new List<ComponentDefinition.EventDefinition>();
                ComponentId = null;
            }

            SourceFile = bundle.TypeToFile[qualifiedName];

            NestedTypes = bundle.Types.Where(t => t.Value.OuterType == qualifiedName).Select(type =>
            {
                var t = bundle.Types[type.Key];
                return new TypeDescription(t.QualifiedName, bundle);
            }).ToList();

            var warnings = new List<string>();
            Warnings = warnings;

            Fields = Fields.Where(f =>
            {
                var allowed = !IsPrimitiveEntityField(f);

                if (!allowed)
                {
                    warnings.Add($"field '{qualifiedName}.{f.Name}' is the Entity type, which is currently unsupported.");
                }

                return allowed;
            }).ToList();
        }

        private static bool IsPrimitiveEntityField(FieldDefinition f)
        {
            // The Entity primitive type is currently unsupported, and undocumented.
            // It is ignored for now.
            return f.TypeSelector switch
            {
                FieldType.Option => (f.OptionType.InnerType.ValueTypeSelector == ValueType.Primitive && f.OptionType.InnerType.Primitive == PrimitiveType.Entity),
                FieldType.List => (f.ListType.InnerType.ValueTypeSelector == ValueType.Primitive && f.ListType.InnerType.Primitive == PrimitiveType.Entity),
                FieldType.Map => (f.MapType.KeyType.ValueTypeSelector == ValueType.Primitive && f.MapType.KeyType.Primitive == PrimitiveType.Entity ||
                                  f.MapType.ValueType.ValueTypeSelector == ValueType.Primitive && f.MapType.ValueType.Primitive == PrimitiveType.Entity),
                FieldType.Singular => (f.SingularType.Type.ValueTypeSelector == ValueType.Primitive && f.SingularType.Type.Primitive == PrimitiveType.Entity),
                _ => false
            };
        }
    }
}
