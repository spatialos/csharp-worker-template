using System;

namespace Improbable.Schema.Bundle
{
    public static class FieldDefinitionExtensions
    {
        public static bool HasEnum(this TypeReference type)
        {
            return type.ValueTypeSelector == ValueType.Enum;
        }

        public static bool HasEnum(this TypeReference type, string qualifiedTypeName)
        {
            return type.ValueTypeSelector == ValueType.Enum && type.Enum == qualifiedTypeName;
        }

        public static bool HasPrimitive(this TypeReference type)
        {
            return type.ValueTypeSelector == ValueType.Primitive;
        }

        public static bool HasPrimitive(this TypeReference type, PrimitiveType primitiveType)
        {
            return type.ValueTypeSelector == ValueType.Primitive && type.Primitive == primitiveType;
        }

        public static bool HasCustomType(this TypeReference type)
        {
            return type.ValueTypeSelector == ValueType.Type;
        }

        public static bool HasCustomType(this TypeReference type, string qualifiedTypeName)
        {
            return type.ValueTypeSelector == ValueType.Type && type.Type == qualifiedTypeName;
        }

        public static bool HasEnum(this FieldDefinition field)
        {
#nullable disable
            return field.TypeSelector switch
            {
                FieldType.Option => field.OptionType.InnerType.HasEnum(),
                FieldType.List => field.ListType.InnerType.HasEnum(),
                FieldType.Map => throw new InvalidOperationException("Invalid for the map type. Check the key and value types individually."),
                FieldType.Singular => field.SingularType.Type.HasEnum(),
                _ => throw new ArgumentOutOfRangeException()
            };
#nullable restore
        }

        public static bool HasPrimitive(this FieldDefinition field)
        {
#nullable disable
            return field.TypeSelector switch
            {
                FieldType.Option => field.OptionType.InnerType.HasPrimitive(),
                FieldType.List => field.ListType.InnerType.HasPrimitive(),
                FieldType.Map => throw new InvalidOperationException("Invalid for the map type. Check the key and value types individually."),
                FieldType.Singular => field.SingularType.Type.HasPrimitive(),
                _ => throw new ArgumentOutOfRangeException()
            };
#nullable restore
        }

        public static bool HasPrimitive(this FieldDefinition field, PrimitiveType type)
        {
#nullable disable
            return field.TypeSelector switch
            {
                FieldType.Option => field.OptionType.InnerType.HasPrimitive(type),
                FieldType.List => field.ListType.InnerType.HasPrimitive(type),
                FieldType.Map => throw new InvalidOperationException("Invalid for the map type. Check the key and value types individually."),
                FieldType.Singular => field.SingularType.Type.HasPrimitive(type),
                _ => throw new ArgumentOutOfRangeException()
            };
#nullable restore
        }

        public static bool HasCustomType(this FieldDefinition field)
        {
#nullable disable
            return field.TypeSelector switch
            {
                FieldType.Option => field.OptionType.InnerType.HasCustomType(),
                FieldType.List => field.ListType.InnerType.HasCustomType(),
                FieldType.Map => throw new InvalidOperationException("Invalid for the map type. Check the key and value types individually."),
                FieldType.Singular => field.SingularType.Type.HasCustomType(),
                _ => throw new ArgumentOutOfRangeException()
            };
#nullable restore

        }

        public static bool HasCustomType(this FieldDefinition field, string qualifiedTypeName)
        {
#nullable disable
            return field.TypeSelector switch
            {
                FieldType.Option => field.OptionType.InnerType.HasCustomType(qualifiedTypeName),
                FieldType.List => field.ListType.InnerType.HasCustomType(qualifiedTypeName),
                FieldType.Map => throw new InvalidOperationException("Invalid for the map type. Check the key and value types individually."),
                FieldType.Singular => field.SingularType.Type.HasCustomType(qualifiedTypeName),
                _ => throw new ArgumentOutOfRangeException()
            };
#nullable restore
        }

        public static bool CanPrimitiveBeNull(this FieldDefinition field)
        {
            return field.HasPrimitive(PrimitiveType.String) || field.HasPrimitive(PrimitiveType.Bytes);
        }

        public static bool IsSingular(this FieldDefinition field)
        {
            return field.TypeSelector == FieldType.Singular;
        }

        public static bool IsOption(this FieldDefinition field)
        {
            return field.TypeSelector == FieldType.Option;
        }

        public static bool IsList(this FieldDefinition field)
        {
            return field.TypeSelector == FieldType.List;
        }

        public static bool IsMap(this FieldDefinition field)
        {
            return field.TypeSelector == FieldType.Map;
        }
    }
}
