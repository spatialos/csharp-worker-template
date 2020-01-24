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
            if (!field.IsOption())
            {
                throw new InvalidOperationException("Must be called for an option<> field");
            }

            return field.OptionType.InnerType.ValueTypeSelector switch
            {
                ValueType.Enum => "?",
                ValueType.Type => "?",
                ValueType.Primitive when field.HasPrimitive(PrimitiveType.String) => string.Empty,
                ValueType.Primitive when field.HasPrimitive(PrimitiveType.Bytes) => string.Empty,
                ValueType.Primitive => "?",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string OptionValueTest(this FieldDefinition field)
        {
            if (!field.IsOption())
            {
                throw new InvalidOperationException("Must be called for an option<> field");
            }

            return field.OptionType.InnerType.ValueTypeSelector switch
            {
                ValueType.Enum => ".HasValue",
                ValueType.Primitive when field.HasPrimitive(PrimitiveType.String) => " != null",
                ValueType.Primitive when field.HasPrimitive(PrimitiveType.Bytes) => " != null",
                ValueType.Primitive => ".HasValue",
                ValueType.Type => ".HasValue",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static string OptionValueSuffix(this FieldDefinition field)
        {
            if (!field.IsOption())
            {
                throw new InvalidOperationException("Must be called for an option<> field");
            }

            return field.OptionType.InnerType.ValueTypeSelector switch
            {
                ValueType.Enum => ".Value",
                ValueType.Type => ".Value",
                ValueType.Primitive when field.HasPrimitive(PrimitiveType.String) => string.Empty,
                ValueType.Primitive when field.HasPrimitive(PrimitiveType.Bytes) => string.Empty,
                ValueType.Primitive => ".Value",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string FqnFieldType(TypeDescription type, FieldDefinition field) =>
            field.TypeSelector switch
            {
                FieldType.Option => $"{field.InnerFqn()}{field.OptionTypeSuffix()}",
                FieldType.Singular => field.InnerFqn(),

                FieldType.List when IsFieldRecursive(type, field) => $"global::System.Collections.Generic.IReadOnlyList<{field.InnerFqn()}>",
                FieldType.Map when IsFieldRecursive(type, field) => $"global::System.Collections.Generic.IReadOnlyDictionary<{field.MapType.KeyType.Fqn()}, {field.MapType.ValueType.Fqn()}>",

                FieldType.List => $"global::System.Collections.Immutable.ImmutableArray<{field.InnerFqn()}>",
                FieldType.Map => $"global::System.Collections.Immutable.ImmutableDictionary<{field.MapType.KeyType.Fqn()}, {field.MapType.ValueType.Fqn()}>",
                _ => throw new ArgumentOutOfRangeException()
            };

        public static string ParameterType(this FieldDefinition field) =>
            field.TypeSelector switch
            {
                FieldType.Option => $"{field.InnerFqn()}{field.OptionTypeSuffix()}",
                FieldType.List => $"global::System.Collections.Generic.IEnumerable<{field.InnerFqn()}>",
                FieldType.Map => $"global::System.Collections.Generic.IEnumerable<global::System.Collections.Generic.KeyValuePair<{field.MapType.KeyType.Fqn()}, {field.MapType.ValueType.Fqn()}>>",
                FieldType.Singular => field.SingularType.Type.Fqn(),
                _ => throw new ArgumentOutOfRangeException()
            };

        public static string GetEmptyCollection(TypeDescription type, FieldDefinition field) =>
            field.TypeSelector switch
            {
                FieldType.List when IsFieldRecursive(type, field) => $"new global::System.Collections.Generic.List<{field.InnerFqn()}>()",
                FieldType.Map when IsFieldRecursive(type, field) => $"new global::System.Collections.Generic.Dictionary<{field.MapType.KeyType.Fqn()}, {field.MapType.ValueType.Fqn()}>()",

                FieldType.List => $"global::System.Collections.Immutable.ImmutableArray<{field.InnerFqn()}>.Empty",
                FieldType.Map => $"global::System.Collections.Immutable.ImmutableDictionary<{field.MapType.KeyType.Fqn()}, {field.MapType.ValueType.Fqn()}>.Empty",

                _ => throw new ArgumentOutOfRangeException(nameof(field.TypeSelector))
            };

        public static string InitializeFromParameter(TypeDescription type, FieldDefinition field) =>
            field.TypeSelector switch
            {
                FieldType.List when IsFieldRecursive(type, field) => $"new global::System.Collections.Generic.List<{field.InnerFqn()}>({field.CamelCase()})",
                FieldType.Map when IsFieldRecursive(type, field) => $"global::System.Linq.Enumerable.ToDictionary({field.CamelCase()}, kv => kv.Key, kv => kv.Value)",

                FieldType.List => $"global::System.Collections.Immutable.ImmutableArray.ToImmutableArray({field.CamelCase()})",
                FieldType.Map => $"global::System.Collections.Immutable.ImmutableDictionary.ToImmutableDictionary({field.CamelCase()})",

                _ => field.CamelCase()
            };

        public static string GetEmptyFieldInstantiationAsCsharp(TypeDescription type, FieldDefinition field) =>
            field.TypeSelector switch
            {
                FieldType.Option => "null",
                FieldType.Singular => field.SingularType.Type.Fqn(),

                FieldType.List when IsFieldRecursive(type, field) => $"new global::System.Collections.Generic.List<{field.InnerFqn()}>()",
                FieldType.Map when IsFieldRecursive(type, field)  => $"new global::System.Collections.Generic.Dictionary<{field.MapType.KeyType.Fqn()}, {field.MapType.ValueType.Fqn()}>()",

                FieldType.List => $"global::System.Collections.Immutable.ImmutableArray<{field.InnerFqn()}>.Empty",
                FieldType.Map => $"global::System.Collections.Immutable.ImmutableDictionary<{field.MapType.KeyType.Fqn()}, {field.MapType.ValueType.Fqn()}>.Empty",

                _ => throw new ArgumentOutOfRangeException()
            };

        public static string PascalCase(this FieldDefinition field) => SnakeCaseToPascalCase(field.Name);

        public static string CamelCase(this FieldDefinition field)
        {
            var name = SnakeCaseToCamelCase(field.Name);
            return !Keywords.Contains(name) ? name : $"@{name}";
        }

        public static string CamelCase(this ComponentDefinition.EventDefinition evt)
        {
            var name = SnakeCaseToCamelCase(evt.Name);
            return !Keywords.Contains(name) ? name : $"@{name}";
        }

        public static string Fqn(this ComponentDefinition.EventDefinition evt) => Case.Fqn(evt.Type);

        public static string Fqn(this TypeReference typeRef) =>
            typeRef.ValueTypeSelector switch
            {
                ValueType.Enum => $"{Case.Fqn(typeRef.Enum)}",
                ValueType.Primitive => typeRef.Primitive switch
                {
                    PrimitiveType.Double => "double",
                    PrimitiveType.Float => "float",
                    PrimitiveType.Int32 => "int",
                    PrimitiveType.Int64 => "long",
                    PrimitiveType.Uint32 => "uint",
                    PrimitiveType.Uint64 => "ulong",
                    PrimitiveType.Sint32 => "int",
                    PrimitiveType.Sint64 => "long",
                    PrimitiveType.Fixed32 => "uint",
                    PrimitiveType.Fixed64 => "ulong",
                    PrimitiveType.Sfixed32 => "int",
                    PrimitiveType.Sfixed64 => "long",
                    PrimitiveType.Bool => "bool",
                    PrimitiveType.String => "string",
                    PrimitiveType.Bytes => "byte[]",
                    PrimitiveType.EntityId => EntityIdType,
                    PrimitiveType.Invalid => throw new InvalidOperationException("Invalid primitive type"),
                    PrimitiveType.Entity => throw new InvalidOperationException("The entity schema type is not supported"),
                    _ => throw new ArgumentOutOfRangeException()
                },
                ValueType.Type => $"{Case.Fqn(typeRef.Type)}",
                _ => throw new ArgumentOutOfRangeException()
            };

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

            const string structuralComparer = "global::System.Collections.StructuralComparisons.StructuralEqualityComparer";

            return field.TypeSelector switch
            {
                FieldType.Option => $"{fieldName} == other.{fieldName}",
                FieldType.List => $"{structuralComparer}.Equals({fieldName}, other.{fieldName})",
                FieldType.Map => $"{structuralComparer}.Equals({fieldName}, other.{fieldName})",

                FieldType.Singular when !field.HasPrimitive() => $"{fieldName} == other.{fieldName}",
                FieldType.Singular => field.SingularType.Type.Primitive switch
                {
                    PrimitiveType.Double => $"{fieldName}.Equals(other.{fieldName})",
                    PrimitiveType.Float => $"{fieldName}.Equals(other.{fieldName})",
                    PrimitiveType.String => $"string.Equals({fieldName}, other.{fieldName})",
                    PrimitiveType.Bytes => $"Equals({fieldName}, other.{fieldName})",
                    PrimitiveType.EntityId => $"{EntityIdType}.Equals({fieldName}, other.{fieldName})",
                    PrimitiveType.Invalid => throw new InvalidOperationException("Invalid primitive type"),
                    PrimitiveType.Entity => throw new InvalidOperationException("The entity schema type is not supported"),
                    _ => $"{fieldName} == other.{fieldName}"
                },
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetFieldGetMethod(FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.List when field.HasCustomType() => "IndexObject",
                FieldType.Map => "IndexObject",
                
                _ when field.HasPrimitive() => $"Get{field.InnerPrimitiveType()}",
                _ when field.HasEnum() => "GetEnum",
                _ when field.HasCustomType() => "GetObject",

                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetFieldAddMethod(FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Map => "AddObject",

                _ when field.HasPrimitive() => $"Add{field.InnerPrimitiveType()}",
                _ when field.HasEnum() => "AddEnum",
                _ when field.HasCustomType() => "AddObject",

                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetFieldCountMethod(FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Map => "GetObjectCount",

                _ when field.HasPrimitive() => $"Get{field.InnerPrimitiveType()}Count",
                _ when field.HasEnum() => "GetEnumCount",
                _ when field.HasCustomType() => "GetObjectCount",

                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string GetFieldIndexMethod(FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Map => "IndexObject",

                _ when field.HasPrimitive() => $"Index{field.InnerPrimitiveType()}",
                _ when field.HasEnum() => "IndexEnum",
                _ when field.HasCustomType() => "IndexObject",

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
            return t.Annotations.Any(a => a.TypeValue.Type == attributeName);
        }

        public static bool IsFieldRecursive(TypeDescription type, FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Map => (field.MapType.KeyType.HasCustomType(type.QualifiedName) || field.MapType.ValueType.HasCustomType(type.QualifiedName)),
                _ => field.HasCustomType(type.QualifiedName)
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

        public static PrimitiveType InnerPrimitiveType(this FieldDefinition field)
        {
            if (!field.HasPrimitive())
            {
                throw new InvalidOperationException("Called on field without a primitive type.");
            }

            return field.TypeSelector switch
            {
                FieldType.Option => field.OptionType.InnerType.Primitive,
                FieldType.List => field.ListType.InnerType.Primitive,
                FieldType.Map => throw new InvalidOperationException("Invalid for the map type. Check the key and value types individually."),
                FieldType.Singular => field.SingularType.Type.Primitive,
                _ => throw new ArgumentOutOfRangeException()
            };
        }


        public static string InnerFqn(this FieldDefinition field)
        {
            return field.TypeSelector switch
            {
                FieldType.Option => field.OptionType.InnerType.Fqn(),
                FieldType.List => field.ListType.InnerType.Fqn(),
                FieldType.Map => $"global::System.Collections.Generic.KeyValuePair<{field.MapType.KeyType.Fqn()}, {field.MapType.ValueType.Fqn()}>",
                FieldType.Singular => field.SingularType.Type.Fqn(),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static string Namespace(this EnumDefinition type) => Case.Namespace(type.QualifiedName);

        public static string Namespace(this TypeDescription type) => Case.Namespace(type.QualifiedName);

        public static string Fqn(this TypeDescription type) => Case.Fqn(type.QualifiedName);

        public static (string request, string response) InnerFqns(this ComponentDefinition.CommandDefinition cmd) => (Case.Fqn(cmd.RequestType), Case.Fqn(cmd.ResponseType));

        public static string Name(this ComponentDefinition.CommandDefinition cmd) => (SnakeCaseToPascalCase(cmd.Name));

        public static string TypeName(this TypeDescription type)
        {
            return SnakeCaseToPascalCase(type.QualifiedName).Split('.').Last();
        }

        public static string Name(this EnumValueDefinition value)
        {
            return value.Name.Split(new[] { "_" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part =>
                {
                    if (part.Length == 1)
                    {
                        return part;
                    }

                    return part[0] + part.Substring(1, part.Length - 1).ToLowerInvariant();
                })
                .Aggregate(string.Empty, (s1, s2) => s1 + s2);
        }
    }
}
