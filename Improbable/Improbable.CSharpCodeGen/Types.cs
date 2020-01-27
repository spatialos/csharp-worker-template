using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Improbable.Schema.Bundle;
using static Improbable.CSharpCodeGen.Case;
using ValueType = Improbable.Schema.Bundle.ValueType;

namespace Improbable.CSharpCodeGen
{
    public static class Types
    {
        public const string GenericIEnumerable = "global::System.Collections.Generic.IEnumerable";
        public const string SchemaComponentUpdate = "global::Improbable.Worker.CInterop.SchemaComponentUpdate";
        public const string EntityIdType = "global::Improbable.Stdlib.EntityId";
        public const string SchemaMapKeyFieldId = "global::Improbable.Worker.CInterop.SchemaObject.SchemaMapKeyFieldId";
        public const string SchemaMapValueFieldId = "global::Improbable.Worker.CInterop.SchemaObject.SchemaMapValueFieldId";

        public static Dictionary<PrimitiveType, string> SchemaToCSharpTypes = new Dictionary<PrimitiveType, string>
        {
            {PrimitiveType.Double, "double"},
            {PrimitiveType.Float, "float"},
            {PrimitiveType.Int32, "int"},
            {PrimitiveType.Int64, "long"},
            {PrimitiveType.Uint32, "uint"},
            {PrimitiveType.Uint64, "ulong"},
            {PrimitiveType.Sint32, "int"},
            {PrimitiveType.Sint64, "long"},
            {PrimitiveType.Fixed32, "uint"},
            {PrimitiveType.Fixed64, "ulong"},
            {PrimitiveType.Sfixed32, "int"},
            {PrimitiveType.Sfixed64, "long"},
            {PrimitiveType.Bool, "bool"},
            {PrimitiveType.String, "string"},
            {PrimitiveType.Bytes, "byte[]"},
            {PrimitiveType.EntityId, EntityIdType}
        };

        public static Dictionary<PrimitiveType, Func<string, string>> SchemaToHashFunction = new Dictionary<PrimitiveType, Func<string, string>>
        {
            {PrimitiveType.Double, f => $"{f}.GetHashCode()"},
            {PrimitiveType.Float, f => $"{f}.GetHashCode()"},
            {PrimitiveType.Int32, f => $"(int){f}"},
            {PrimitiveType.Int64, f => $"(int){f}"},
            {PrimitiveType.Uint32, f => $"(int){f}"},
            {PrimitiveType.Uint64, f => $"(int){f}"},
            {PrimitiveType.Sint32, f => f},
            {PrimitiveType.Sint64, f => $"(int){f}"},
            {PrimitiveType.Fixed32, f => $"(int){f}"},
            {PrimitiveType.Fixed64, f => $"(int){f}"},
            {PrimitiveType.Sfixed32, f => f},
            {PrimitiveType.Sfixed64, f => $"(int){f}"},
            {PrimitiveType.Bool, f => $"{f}.GetHashCode()"},
            {PrimitiveType.String, f => $"{f} != null ? {f}.GetHashCode() : 0"},
            {PrimitiveType.Bytes, f => $"{f}.GetHashCode()"},
            {PrimitiveType.EntityId, f => $"{f}.GetHashCode()"}
        };

        public static Dictionary<PrimitiveType, Func<string, string>> SchemaToEqualsFunction = new Dictionary<PrimitiveType, Func<string, string>>
        {
            {PrimitiveType.Double, f => $"{f}.Equals(other.{f})"},
            {PrimitiveType.Float, f => $"{f}.Equals(other.{f})"},
            {PrimitiveType.Int32, f => $"{f} == other.{f}"},
            {PrimitiveType.Int64, f => $"{f} == other.{f}"},
            {PrimitiveType.Uint32, f => $"{f} == other.{f}"},
            {PrimitiveType.Uint64, f => $"{f} == other.{f}"},
            {PrimitiveType.Sint32, f => $"{f} == other.{f}"},
            {PrimitiveType.Sint64, f => $"{f} == other.{f}"},
            {PrimitiveType.Fixed32, f => $"{f} == other.{f}"},
            {PrimitiveType.Fixed64, f => $"{f} == other.{f}"},
            {PrimitiveType.Sfixed32, f => $"{f} == other.{f}"},
            {PrimitiveType.Sfixed64, f => $"{f} == other.{f}"},
            {PrimitiveType.Bool, f => $"{f} == other.{f}"},
            {PrimitiveType.String, f => $"string.Equals({f}, other.{f})"},
            {PrimitiveType.Bytes, f => $"Equals({f}, other.{f})"},
            {PrimitiveType.EntityId, f => $"{EntityIdType}.Equals({f}, other.{f})"}
        };

        public static HashSet<string> Keywords = new HashSet<string>
        {
            "abstract",
            "as",
            "base",
            "bool",
            "break",
            "byte",
            "case",
            "catch",
            "char",
            "checked",
            "class",
            "const",
            "continue",
            "decimal",
            "default",
            "delegate",
            "do",
            "double",
            "else",
            "enum",
            "event",
            "explicit",
            "extern",
            "false",
            "finally",
            "fixed",
            "float",
            "for",
            "foreach",
            "goto",
            "if",
            "implicit",
            "in",
            "int",
            "interface",
            "internal",
            "is",
            "lock",
            "long",
            "namespace",
            "new",
            "null",
            "object",
            "operator",
            "out",
            "override",
            "params",
            "private",
            "protected",
            "public",
            "readonly",
            "ref",
            "return",
            "sbyte",
            "sealed",
            "short",
            "sizeof",
            "stackalloc",
            "static",
            "string",
            "struct",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "uint",
            "ulong",
            "unchecked",
            "unsafe",
            "ushort",
            "using",
            "var",
            "virtual",
            "void",
            "volatile",
            "when",
            "while"
        };

        public static string OptionTypeSuffix(this FieldDefinition field)
        {
            return type.ValueTypeSelector switch
            {
                ValueType.Enum => "?",
                ValueType.Primitive => (type.Primitive switch
                {
                    PrimitiveType.String => string.Empty,
                    PrimitiveType.Bytes => string.Empty,
                    _ => "?"
                }),
                ValueType.Type => "?",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string OptionValueTest(this FieldDefinition field)
        {
            return type.ValueTypeSelector switch
            {
                ValueType.Enum => ".HasValue",
                ValueType.Primitive => (type.Primitive switch
                {
                    PrimitiveType.String => " != null",
                    PrimitiveType.Bytes => " != null",
                    _ => ".HasValue"
                }),
                ValueType.Type => ".HasValue",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static string OptionValueSuffix(this FieldDefinition field)
        {
            return type.ValueTypeSelector switch
            {
                ValueType.Enum => ".Value",
                ValueType.Primitive => (type.Primitive switch
                {
                    PrimitiveType.String => string.Empty,
                    PrimitiveType.Bytes => string.Empty,
                    _ => ".Value"
                }),
                ValueType.Type => ".Value",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetFieldTypeAsCsharp(TypeDescription type, FieldDefinition field)
        {
            if (field.TypeSelector == FieldType.Option)
            {
                return $"{TypeReferenceToType(field.OptionType.InnerType)}{GetOptionType(field.OptionType.InnerType)}";
            }

            if (field.TypeSelector == FieldType.Singular)
            {
                return TypeReferenceToType(field.SingularType.Type);
            }

            if (IsFieldRecursive(type, field))
            {
                return field.TypeSelector switch
                {
                    FieldType.List => $"global::System.Collections.Generic.IReadOnlyList<{TypeReferenceToType(field.ListType.InnerType)}>",
                    FieldType.Map => $"global::System.Collections.Generic.IReadOnlyDictionary<{TypeReferenceToType(field.MapType.KeyType)}, {TypeReferenceToType(field.MapType.ValueType)}>",
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            return field.TypeSelector switch
            {
                FieldType.List => $"global::System.Collections.Immutable.ImmutableArray<{TypeReferenceToType(field.ListType.InnerType)}>",
                FieldType.Map => $"global::System.Collections.Immutable.ImmutableDictionary<{TypeReferenceToType(field.MapType.KeyType)}, {TypeReferenceToType(field.MapType.ValueType)}>",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetParameterTypeAsCsharp(FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Option => $"{TypeReferenceToType(field.OptionType.InnerType)}{GetOptionType(field.OptionType.InnerType)}",
                FieldType.List => $"global::System.Collections.Generic.IEnumerable<{TypeReferenceToType(field.ListType.InnerType)}>",
                FieldType.Map => $"global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.KeyValuePair<{TypeReferenceToType(field.MapType.KeyType)}, {TypeReferenceToType(field.MapType.ValueType)}>>",
                FieldType.Singular => TypeReferenceToType(field.SingularType.Type),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string EmptyCollection(TypeDescription type, FieldDefinition field)
        {
            if (IsFieldRecursive(type, field))
            {
                return field.TypeSelector switch
                {
                    FieldType.List => $"new global::System.Collections.Generic.List<{TypeReferenceToType(field.ListType.InnerType)}>()",
                    FieldType.Map => $"new global::System.Collections.Generic.Dictionary<{TypeReferenceToType(field.MapType.KeyType)}, {TypeReferenceToType(field.MapType.ValueType)}>()",
                    _ => throw new ArgumentOutOfRangeException(nameof(field.TypeSelector))
                };
            }

            return field.TypeSelector switch
            {
                FieldType.List => $"global::System.Collections.Immutable.ImmutableArray<{TypeReferenceToType(field.ListType.InnerType)}>.Empty",
                FieldType.Map => $"global::System.Collections.Immutable.ImmutableDictionary<{TypeReferenceToType(field.MapType.KeyType)}, {TypeReferenceToType(field.MapType.ValueType)}>.Empty",
                _ => throw new ArgumentOutOfRangeException(nameof(field.TypeSelector))
            };
        }

        public static string ParameterConversion(TypeDescription type, FieldDefinition field)
        {
            if (IsFieldRecursive(type, field))
            {
                return field.TypeSelector switch
                {
                    FieldType.List => $"new global::System.Collections.Generic.List<{TypeReferenceToType(field.ListType.InnerType)}>({FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))})",
                    FieldType.Map => $"global::System.Linq.Enumerable.ToDictionary({FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))}, kv => kv.Key, kv => kv.Value)",
                    _ => $"{FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))}"
                };
            }

            return field.TypeSelector switch
            {
                FieldType.List => $"global::System.Collections.Immutable.ImmutableArray.ToImmutableArray({FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))})",
                FieldType.Map => $"global::System.Collections.Immutable.ImmutableDictionary.ToImmutableDictionary({FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))})",
                _ => $"{FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))}"
            };
        }

        public static string PascalCase(this FieldDefinition field) => SnakeCaseToPascalCase(field.Name);

        public static string CamelCase(this FieldDefinition field)
        {
            return type.ValueTypeSelector switch
            {
                ValueType.Enum => CapitalizeNamespace(type.Enum),
                ValueType.Primitive => SchemaToCSharpTypes[type.Primitive],
                ValueType.Type => CapitalizeNamespace(type.Type),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string CamelCase(this ComponentDefinition.EventDefinition evt)
        {
            switch (field.TypeSelector)
            {
                case FieldType.Option:
                    return "null";
                case FieldType.Singular:
                    return TypeReferenceToType(field.SingularType.Type);
            }

            if (IsFieldRecursive(type, field))
            {
                return field.TypeSelector switch
                {
                    FieldType.List => $"new global::System.Collections.Generic.List<{TypeReferenceToType(field.ListType.InnerType)}>()",
                    FieldType.Map => $"new global::System.Collections.Generic.Dictionary<{TypeReferenceToType(field.MapType.KeyType)}, {TypeReferenceToType(field.MapType.ValueType)}>()",
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            return field.TypeSelector switch
            {
                FieldType.List => $"global::System.Collections.Immutable.ImmutableArray<{TypeReferenceToType(field.ListType.InnerType)}>.Empty",
                FieldType.Map => $"global::System.Collections.Immutable.ImmutableDictionary<{TypeReferenceToType(field.MapType.KeyType)}, {TypeReferenceToType(field.MapType.ValueType)}>.Empty",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string TypeReferenceToType(TypeReference typeRef)
        {
            return typeRef.ValueTypeSelector switch
            {
                ValueType.Enum => $"global::{CapitalizeNamespace(typeRef.Enum)}",
                ValueType.Primitive => SchemaToCSharpTypes[typeRef.Primitive],
                ValueType.Type => $"global::{CapitalizeNamespace(typeRef.Type)}",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string FieldToHash(FieldDefinition field)
        {
            var fieldName = field.PascalCase();

            return field.TypeSelector switch
            {
                FieldType.Option => $"{fieldName}{field.OptionValueTest()} ? {fieldName}{field.OptionValueSuffix()}.GetHashCode() : 0",
                FieldType.List => $"{fieldName} == null ? {fieldName}.GetHashCode() : 0",
                FieldType.Map => $"{fieldName} == null ? {fieldName}.GetHashCode() : 0",
                FieldType.Singular when !field.HasPrimitive() => $"{fieldName}.GetHashCode()",
                FieldType.Singular => field.SingularType.Type.Primitive switch
                {
                    PrimitiveType.Double => $"{fieldName}.GetHashCode()",
                    PrimitiveType.Float => $"{fieldName}.GetHashCode()",
                    PrimitiveType.Sint32 => fieldName,
                    PrimitiveType.Sfixed32 => fieldName,
                    PrimitiveType.Bool => $"{fieldName}.GetHashCode()",
                    PrimitiveType.String => $"{fieldName} != null ? {fieldName}.GetHashCode() : 0",
                    PrimitiveType.Bytes => $"{fieldName}.GetHashCode()",
                    PrimitiveType.EntityId => $"{fieldName}.GetHashCode()",
                    PrimitiveType.Invalid => throw new InvalidOperationException("Invalid primitive type"),
                    PrimitiveType.Entity => throw new InvalidOperationException("The entity schema type is not supported"),
                    _ => $"(int) {fieldName}"
                },
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string FieldToEquals(FieldDefinition field)
        {
            var fieldName = field.PascalCase();

            switch (field.TypeSelector)
            {
                case FieldType.Option:
                    return $"{fieldName} == other.{fieldName}";
                case FieldType.List:
                case FieldType.Map:
                    return $"global::System.Collections.StructuralComparisons.StructuralEqualityComparer.Equals({fieldName}, other.{fieldName})";
                case FieldType.Singular:
                    return field.SingularType.Type.Primitive switch
                    {
                        PrimitiveType.Invalid => $"{fieldName} == other.{fieldName}",
                        _ => SchemaToEqualsFunction[field.SingularType.Type.Primitive](fieldName)
                    };

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static string GetFieldGetMethod(FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Option => (field.OptionType.InnerType.ValueTypeSelector switch
                {
                    ValueType.Enum => "GetEnum",
                    ValueType.Primitive => $"Get{field.OptionType.InnerType.Primitive}",
                    ValueType.Type => "GetObject",
                    _ => throw new ArgumentOutOfRangeException()
                }),
                FieldType.List => (field.ListType.InnerType.ValueTypeSelector switch
                {
                    ValueType.Enum => "GetEnumList",
                    ValueType.Primitive => $"Get{field.ListType.InnerType.Primitive}List",
                    ValueType.Type => "IndexObject",
                    _ => throw new ArgumentOutOfRangeException()
                }),
                FieldType.Map => "IndexObject",
                FieldType.Singular => (field.SingularType.Type.ValueTypeSelector switch
                {
                    ValueType.Enum => "GetEnum",
                    ValueType.Primitive => $"Get{field.SingularType.Type.Primitive}",
                    ValueType.Type => "GetObject",
                    _ => throw new ArgumentOutOfRangeException()
                }),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetFieldAddMethod(FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Option => (field.OptionType.InnerType.ValueTypeSelector switch
                {
                    ValueType.Enum => "AddEnum",
                    ValueType.Primitive => $"Add{field.OptionType.InnerType.Primitive}",
                    ValueType.Type => "AddObject",
                    _ => throw new ArgumentOutOfRangeException()
                }),
                FieldType.List => (field.ListType.InnerType.ValueTypeSelector switch
                {
                    ValueType.Enum => "AddEnumList",
                    ValueType.Primitive => $"Add{field.ListType.InnerType.Primitive}List",
                    ValueType.Type => "AddObject",
                    _ => throw new ArgumentOutOfRangeException()
                }),
                FieldType.Map => "GetObject",
                FieldType.Singular => (field.SingularType.Type.ValueTypeSelector switch
                {
                    ValueType.Enum => "AddEnum",
                    ValueType.Primitive => $"Add{field.SingularType.Type.Primitive}",
                    ValueType.Type => "AddObject",
                    _ => throw new ArgumentOutOfRangeException()
                }),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetFieldCountMethod(FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Option => (field.OptionType.InnerType.ValueTypeSelector switch
                {
                    ValueType.Enum => "GetEnumCount",
                    ValueType.Primitive => $"Get{field.OptionType.InnerType.Primitive}Count",
                    ValueType.Type => "GetObjectCount",
                    _ => throw new ArgumentOutOfRangeException()
                }),
                FieldType.List => (field.ListType.InnerType.ValueTypeSelector switch
                {
                    ValueType.Enum => "GetEnumCount",
                    ValueType.Primitive => $"Get{field.ListType.InnerType.Primitive}Count",
                    ValueType.Type => "GetObjectCount",
                    _ => throw new ArgumentOutOfRangeException()
                }),
                FieldType.Map => "GetObjectCount",
                FieldType.Singular => (field.SingularType.Type.ValueTypeSelector switch
                {
                    ValueType.Enum => "GetEnumCount",
                    ValueType.Primitive => $"Get{field.SingularType.Type.Primitive}Count",
                    ValueType.Type => "GetObjectCount",
                    _ => throw new ArgumentOutOfRangeException()
                }),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string TypeToFilename(string qualifiedName)
        {
            var path = qualifiedName.Split('.');

            var folder = Path.Combine(path.Take(path.Length - 1).Select(SnakeCaseToPascalCase).Select(CapitalizeFirstLetter).ToArray());
            return Path.Combine(folder, $"{path.Last()}.g.cs");
        }

        public static bool HasAnnotation(TypeDescription t, string attributeName)
        {
            return !Keywords.Contains(name) ? name : $"@{name}";
        }

        public static string GetTypeReferenceGetter(TypeReference type)
        {
            return type.ValueTypeSelector switch
            {
                ValueType.Enum => "GetEnum",
                ValueType.Primitive => $"Get{type.Primitive}",
                ValueType.Type => "GetObject",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static PrimitiveType InnerPrimitiveType(this FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Option => (field.OptionType.InnerType.ValueTypeSelector == ValueType.Type && field.OptionType.InnerType.Type == type.QualifiedName),
                FieldType.List => (field.ListType.InnerType.ValueTypeSelector == ValueType.Type && field.ListType.InnerType.Type == type.QualifiedName),
                FieldType.Map => (field.MapType.KeyType.ValueTypeSelector == ValueType.Type && field.MapType.KeyType.Type == type.QualifiedName ||
                                  field.MapType.ValueType.ValueTypeSelector == ValueType.Type && field.MapType.ValueType.Type == type.QualifiedName),
                FieldType.Singular => (field.SingularType.Type.ValueTypeSelector == ValueType.Type && field.SingularType.Type.Type == type.QualifiedName),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static bool IsFieldTypeRecursive(Bundle bundle, string typeQualifiedName, FieldDefinition fieldDefinition)
        {
            return fieldDefinition.TypeSelector == FieldType.Option &&
                   fieldDefinition.OptionType.InnerType.ValueTypeSelector == ValueType.Type &&
                   (fieldDefinition.OptionType.InnerType.Type == typeQualifiedName ||
                    bundle.Types[fieldDefinition.OptionType.InnerType.Type].Fields.Any(f => IsFieldTypeRecursive(bundle, typeQualifiedName, f)));
        }
    }
}
