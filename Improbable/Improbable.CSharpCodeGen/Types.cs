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

        private static string GetTypeSuffix(TypeReference type)
        {
            return type.ValueTypeSelector switch
            {
                ValueType.Enum => "?",
                ValueType.Type => "?",
                ValueType.Primitive when type.Primitive == PrimitiveType.String => string.Empty,
                ValueType.Primitive when type.Primitive == PrimitiveType.Bytes => string.Empty,
                ValueType.Primitive => "?",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static string GetValueTestSuffix(TypeReference type)
        {
            return type.ValueTypeSelector switch
            {
                ValueType.Enum => ".HasValue",
                ValueType.Primitive when type.Primitive == PrimitiveType.String => " != null",
                ValueType.Primitive when type.Primitive == PrimitiveType.Bytes => " != null",
                ValueType.Primitive => ".HasValue",
                ValueType.Type => ".HasValue",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static string GetValueSuffix(TypeReference type)
        {
            return type.ValueTypeSelector switch
            {
                ValueType.Enum => ".Value",
                ValueType.Type => ".Value",
                ValueType.Primitive when type.Primitive == PrimitiveType.String => string.Empty,
                ValueType.Primitive when type.Primitive == PrimitiveType.Bytes => string.Empty,
                ValueType.Primitive => ".Value",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetFieldTypeAsCsharp(TypeDescription type, FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Option => $"{TypeReferenceToType(field.OptionType.InnerType)}{GetTypeSuffix(field.OptionType.InnerType)}",
                FieldType.Singular => TypeReferenceToType(field.SingularType.Type),

                FieldType.List when IsFieldRecursive(type, field) => $"global::System.Collections.Generic.IReadOnlyList<{TypeReferenceToType(field.ListType.InnerType)}>",
                FieldType.Map when IsFieldRecursive(type, field) => $"global::System.Collections.Generic.IReadOnlyDictionary<{TypeReferenceToType(field.MapType.KeyType)}, {TypeReferenceToType(field.MapType.ValueType)}>",

                FieldType.List => $"global::System.Collections.Immutable.ImmutableArray<{TypeReferenceToType(field.ListType.InnerType)}>",
                FieldType.Map => $"global::System.Collections.Immutable.ImmutableDictionary<{TypeReferenceToType(field.MapType.KeyType)}, {TypeReferenceToType(field.MapType.ValueType)}>",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetParameterTypeAsCsharp(FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Option => $"{TypeReferenceToType(field.OptionType.InnerType)}{GetTypeSuffix(field.OptionType.InnerType)}",
                FieldType.List => $"global::System.Collections.Generic.IEnumerable<{TypeReferenceToType(field.ListType.InnerType)}>",
                FieldType.Map => $"global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.KeyValuePair<{TypeReferenceToType(field.MapType.KeyType)}, {TypeReferenceToType(field.MapType.ValueType)}>>",
                FieldType.Singular => TypeReferenceToType(field.SingularType.Type),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetEmptyCollection(TypeDescription type, FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.List when IsFieldRecursive(type, field) => $"new global::System.Collections.Generic.List<{TypeReferenceToType(field.ListType.InnerType)}>()",
                FieldType.Map when IsFieldRecursive(type, field) => $"new global::System.Collections.Generic.Dictionary<{TypeReferenceToType(field.MapType.KeyType)}, {TypeReferenceToType(field.MapType.ValueType)}>()",

                FieldType.List => $"global::System.Collections.Immutable.ImmutableArray<{TypeReferenceToType(field.ListType.InnerType)}>.Empty",
                FieldType.Map => $"global::System.Collections.Immutable.ImmutableDictionary<{TypeReferenceToType(field.MapType.KeyType)}, {TypeReferenceToType(field.MapType.ValueType)}>.Empty",

                _ => throw new ArgumentOutOfRangeException(nameof(field.TypeSelector))
            };
        }

        public static string InitializeFromParameter(TypeDescription type, FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.List when IsFieldRecursive(type, field) => $"new global::System.Collections.Generic.List<{TypeReferenceToType(field.ListType.InnerType)}>({FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))})",
                FieldType.Map when IsFieldRecursive(type, field) => $"global::System.Linq.Enumerable.ToDictionary({FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))}, kv => kv.Key, kv => kv.Value)",

                FieldType.List => $"global::System.Collections.Immutable.ImmutableArray.ToImmutableArray({FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))})",
                FieldType.Map => $"global::System.Collections.Immutable.ImmutableDictionary.ToImmutableDictionary({FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))})",

                _ => $"{FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))}"
            };
        }

        public static string GetTypeAsCsharp(TypeReference type)
        {
            return type.ValueTypeSelector switch
            {
                ValueType.Enum => CapitalizeNamespace(type.Enum),
                ValueType.Primitive => SchemaToCSharpTypes[type.Primitive],
                ValueType.Type => CapitalizeNamespace(type.Type),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetEmptyFieldInstantiationAsCsharp(TypeDescription type, FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Option => "null",
                FieldType.Singular => TypeReferenceToType(field.SingularType.Type),

                FieldType.List when IsFieldRecursive(type, field) => $"new global::System.Collections.Generic.List<{TypeReferenceToType(field.ListType.InnerType)}>()",
                FieldType.Map when IsFieldRecursive(type, field)  => $"new global::System.Collections.Generic.Dictionary<{TypeReferenceToType(field.MapType.KeyType)}, {TypeReferenceToType(field.MapType.ValueType)}>()",

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
            var fieldName = SnakeCaseToPascalCase(field.Name);

            return field.TypeSelector switch
            {
                FieldType.Option => $"{fieldName}{GetValueTestSuffix(field.OptionType.InnerType)} ? {fieldName}{GetValueSuffix(field.OptionType.InnerType)}.GetHashCode() : 0",
                FieldType.List => $"{fieldName} == null ? {fieldName}.GetHashCode() : 0",
                FieldType.Map => $"{fieldName} == null ? {fieldName}.GetHashCode() : 0",
                FieldType.Singular when !field.HasPrimitive() => $"{fieldName}.GetHashCode()",
                FieldType.Singular => SchemaToHashFunction[field.SingularType.Type.Primitive](fieldName),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string FieldToEquals(FieldDefinition field)
        {
            var fieldName = SnakeCaseToPascalCase(field.Name);

            return field.TypeSelector switch
            {
                FieldType.Option => $"{fieldName} == other.{fieldName}",
                FieldType.List => $"global::System.Collections.StructuralComparisons.StructuralEqualityComparer.Equals({fieldName}, other.{fieldName})",
                FieldType.Map => $"global::System.Collections.StructuralComparisons.StructuralEqualityComparer.Equals({fieldName}, other.{fieldName})",
                FieldType.Singular when !field.HasPrimitive() => $"{fieldName} == other.{fieldName}",
                FieldType.Singular => SchemaToEqualsFunction[field.SingularType.Type.Primitive](fieldName),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetFieldGetMethod(FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Option when field.HasEnum() => "GetEnum",
                FieldType.Option when field.HasPrimitive() => $"Get{field.OptionType.InnerType.Primitive}",
                FieldType.Option when field.HasCustomType() => "GetObject",

                FieldType.List when field.HasEnum() => "GetEnum",
                FieldType.List when field.HasPrimitive() => $"Get{field.ListType.InnerType.Primitive}",
                FieldType.List when field.HasCustomType() => "IndexObject",

                FieldType.Map => "IndexObject",

                FieldType.Singular when field.HasEnum() => "GetEnum",
                FieldType.Singular when field.HasPrimitive() => $"Get{field.SingularType.Type.Primitive}",
                FieldType.Singular when field.HasCustomType() => "GetObject",

                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetFieldAddMethod(FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Option when field.HasEnum() => "AddEnum",
                FieldType.Option when field.HasPrimitive() => $"Add{field.OptionType.InnerType.Primitive}",
                FieldType.Option when field.HasCustomType() => "AddObject",

                FieldType.List when field.HasEnum() => "AddEnum",
                FieldType.List when field.HasPrimitive(PrimitiveType.String) => "AddString",
                FieldType.List when field.HasPrimitive(PrimitiveType.Bytes) => "AddBytes",
                FieldType.List when field.HasPrimitive() => $"Add{field.ListType.InnerType.Primitive}",
                FieldType.List when field.HasCustomType() => "AddObject",

                FieldType.Map => "AddObject",

                FieldType.Singular when field.HasEnum() => "AddEnum",
                FieldType.Singular when field.HasPrimitive() => $"Add{field.SingularType.Type.Primitive}",
                FieldType.Singular when field.HasCustomType() => "AddObject",

                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetFieldCountMethod(FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Option when field.HasEnum() => "GetEnumCount",
                FieldType.Option when field.HasPrimitive() => $"Get{field.OptionType.InnerType.Primitive}Count",
                FieldType.Option when field.HasCustomType() => "GetObjectCount",

                FieldType.List when field.HasEnum() => "GetEnumCount",
                FieldType.List when field.HasPrimitive() => $"Get{field.ListType.InnerType.Primitive}Count",
                FieldType.List when field.HasCustomType() => "GetObjectCount",

                FieldType.Map => "GetObjectCount",

                FieldType.Singular when field.HasEnum() => "GetEnumCount",
                FieldType.Singular when field.HasPrimitive() => $"Get{field.SingularType.Type.Primitive}Count",
                FieldType.Singular when field.HasCustomType() => "GetObjectCount",

                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetFieldIndexMethod(FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Option when field.HasEnum() => "IndexEnum",
                FieldType.Option when field.HasPrimitive() => $"Index{field.OptionType.InnerType.Primitive}",
                FieldType.Option when field.HasCustomType() => "IndexObject",

                FieldType.List when field.HasEnum() => "IndexEnum",
                FieldType.List when field.HasPrimitive() => $"Index{field.ListType.InnerType.Primitive}",
                FieldType.List when field.HasCustomType() => "IndexObject",

                FieldType.Map => "IndexObject",

                FieldType.Singular when field.HasEnum() => "IndexEnum",
                FieldType.Singular when field.HasPrimitive() => $"Index{field.SingularType.Type.Primitive}",
                FieldType.Singular when field.HasCustomType() => "IndexObject",

                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string TypeToFilename(string qualifiedName)
        {
            var path = qualifiedName.Split('.');

            var folder = Path.Combine(path.Take(path.Length - 1).Select(SnakeCaseToPascalCase).Select(CapitalizeFirstLetter).ToArray());
            return Path.Combine(folder, $"{path.Last()}.g.cs");
        }

        public static string FieldNameToSafeName(string name)
        {
            return !Keywords.Contains(name) ? name : $"@{name}";
        }

        public static bool HasAnnotation(TypeDescription t, string attributeName)
        {
            return t.Annotations.Any(a => a.TypeValue.Type == attributeName);
        }

        public static bool IsFieldRecursive(TypeDescription type, FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Map => (field.MapType.KeyType.HasCustomType(type.QualifiedName) || field.MapType.ValueType.HasCustomType(type.QualifiedName)),
                _ => field.HasCustomType(type.QualifiedName),
            };
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


        public static string GetInnerTypeAsCsharp(FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Option => TypeReferenceToType(field.OptionType.InnerType),
                FieldType.List => TypeReferenceToType(field.ListType.InnerType),
                FieldType.Map => $"global::System.Collections.Generic.KeyValuePair<{TypeReferenceToType(field.MapType.KeyType)}, {TypeReferenceToType(field.MapType.ValueType)}>",
                FieldType.Singular => TypeReferenceToType(field.SingularType.Type),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
