using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Improbable.Schema.Bundle
{
    public enum PrimitiveType
    {
        Invalid = 0,
        Int32 = 1,
        Int64 = 2,
        Uint32 = 3,
        Uint64 = 4,
        Sint32 = 5,
        Sint64 = 6,
        Fixed32 = 7,
        Fixed64 = 8,
        Sfixed32 = 9,
        Sfixed64 = 10,
        Bool = 11,
        Float = 12,
        Double = 13,
        String = 14,
        EntityId = 15,
        Bytes = 16,
        Entity = 17
    }

    public enum ValueType
    {
        Enum,
        Primitive,
        Type
    }

    public class TypeReference
    {
        public string? Enum;
        public PrimitiveType Primitive;
        public string? Type;

        public ValueType ValueTypeSelector
        {
            get
            {
                if (Primitive != PrimitiveType.Invalid)
                {
                    return ValueType.Primitive;
                }

                if (Type != null)
                {
                    return ValueType.Type;
                }

                if (Enum != null)
                {
                    return ValueType.Enum;
                }

                throw new InvalidOperationException("TypeReference doesn't have any type set.");
            }
        }
    }

    public class Value
    {
        public bool? BoolValue;
        public string? BytesValue;
        public double? DoubleValue;
        public long? EntityIdValue;
        public SchemaEnumValue? EnumValue;
        public float? FloatValue;
        public int? Int32Value;
        public long? Int64Value;
        public ListValueHolder? ListValue;
        public MapValueHolder? MapValue;
        public OptionValueHolder? OptionValue;
        public SourceReference? SourceReference;
        public string? StringValue;
        public TypeValue? TypeValue;
        public uint? Uint32Value;
        public ulong? Uint64Value;

        public class OptionValueHolder
        {
            public Value Value = new Value();
        }

        public class ListValueHolder
        {
            public ImmutableArray<Value> Values = ImmutableArray<Value>.Empty;
        }

        public class MapValueHolder
        {
            public ImmutableArray<MapPairValue> Values = ImmutableArray<MapPairValue>.Empty;

            public class MapPairValue
            {
                public Value Key = new Value();
                public Value Value = new Value();
            }
        }
    }

    public class SchemaEnumValue
    {
        public string Enum = string.Empty;
        public string EnumValue = string.Empty;

        public string Name = string.Empty;
        public string Value = string.Empty;
    }

    [DebuggerDisplay("{" + nameof(Type) + "}")]
    public class TypeValue
    {
        public ImmutableArray<FieldValue> Fields = ImmutableArray<FieldValue>.Empty;

        public string Type = string.Empty;

        [DebuggerDisplay("{" + nameof(Name) + "}")]
        public class FieldValue
        {
            public string Name = string.Empty;
            public SourceReference SourceReference = new SourceReference();
            public Value Value = new Value();
        }
    }

    [DebuggerDisplay("{" + nameof(TypeValue) + "}")]
    public class Annotation
    {
        public SourceReference SourceReference = new SourceReference();
        public TypeValue TypeValue = new TypeValue();
    }

    public class EnumValueDefinition
    {
        public ImmutableArray<Annotation> Annotations = ImmutableArray<Annotation>.Empty;
        public string Name = string.Empty;
        public SourceReference SourceReference = new SourceReference();

        public uint Value;
    }

    [DebuggerDisplay("{" + nameof(QualifiedName) + "}")]
    public class EnumDefinition
    {
        public ImmutableArray<Annotation> Annotations = ImmutableArray<Annotation>.Empty;
        public string Name = string.Empty;
        public string OuterType = string.Empty;
        public string QualifiedName = string.Empty;
        public SourceReference SourceReference = new SourceReference();
        public ImmutableArray<EnumValueDefinition> Values = ImmutableArray<EnumValueDefinition>.Empty;
    }

    public enum FieldType
    {
        Option,
        List,
        Map,
        Singular
    }

    [DebuggerDisplay("{" + nameof(Name) + "}" + " ({" + nameof(FieldId) + "})")]
    public class FieldDefinition
    {
        public ImmutableArray<Annotation> Annotations = ImmutableArray<Annotation>.Empty;

        public string Name = string.Empty;
        public uint FieldId;

        public ListTypeRef? ListType;

        public MapTypeRef? MapType;
        public OptionTypeRef? OptionType;
        public SingularTypeRef? SingularType;

        public SourceReference SourceReference = new SourceReference();

        public bool Transient;

        public FieldType TypeSelector
        {
            get
            {
                if (SingularType != null)
                {
                    return FieldType.Singular;
                }

                if (OptionType != null)
                {
                    return FieldType.Option;
                }

                if (ListType != null)
                {
                    return FieldType.List;
                }

                if (MapType != null)
                {
                    return FieldType.Map;
                }

                throw new InvalidOperationException("FieldType has no types set.");
            }
        }

        public class SingularTypeRef
        {
            public TypeReference Type = new TypeReference();
        }

        public class OptionTypeRef
        {
            public TypeReference InnerType = new TypeReference();
        }

        public class ListTypeRef
        {
            public TypeReference InnerType = new TypeReference();
        }

        public class MapTypeRef
        {
            public TypeReference KeyType = new TypeReference();
            public TypeReference ValueType = new TypeReference();
        }
    }

    [DebuggerDisplay("{" + nameof(QualifiedName) + "}")]
    public class TypeDefinition
    {
        public ImmutableArray<Annotation> Annotations = ImmutableArray<Annotation>.Empty;
        public ImmutableArray<FieldDefinition> Fields = ImmutableArray<FieldDefinition>.Empty;
        public string Name = string.Empty;
        public string OuterType = string.Empty;
        public string QualifiedName = string.Empty;
        public SourceReference SourceReference = new SourceReference();
    }

    [DebuggerDisplay("{" + nameof(QualifiedName) + "} {" + nameof(ComponentId) + "}")]
    public class ComponentDefinition
    {
        public ImmutableArray<Annotation> Annotations = ImmutableArray<Annotation>.Empty;
        public ImmutableArray<CommandDefinition> Commands = ImmutableArray<CommandDefinition>.Empty;
        public uint ComponentId;

        public string DataDefinition = string.Empty;
        public ImmutableArray<EventDefinition> Events = ImmutableArray<EventDefinition>.Empty;

        public ImmutableArray<FieldDefinition> Fields = ImmutableArray<FieldDefinition>.Empty;
        public string Name = string.Empty;

        public string QualifiedName = string.Empty;
        public SourceReference SourceReference = new SourceReference();

        [DebuggerDisplay("{" + nameof(QualifiedName) + "}")]
        public class EventDefinition
        {
            public ImmutableArray<Annotation> Annotations = ImmutableArray<Annotation>.Empty;
            public uint EventIndex;
            public string Name = string.Empty;
            public SourceReference SourceReference = new SourceReference();
            public string Type = string.Empty;
        }

        [DebuggerDisplay("{" + nameof(QualifiedName) + "}" + " {" + nameof(CommandIndex) + "}")]
        public class CommandDefinition
        {
            public ImmutableArray<Annotation> Annotations = ImmutableArray<Annotation>.Empty;
            public uint CommandIndex;

            public string Name = string.Empty;
            public string RequestType = string.Empty;
            public string ResponseType = string.Empty;
            public SourceReference SourceReference = new SourceReference();
        }
    }

    public class SourceReference
    {
        public uint Column;
        public uint Line;
    }

    public class SchemaBundle
    {
        public ImmutableArray<SchemaFile> SchemaFiles = ImmutableArray<SchemaFile>.Empty;
    }

    public class Package
    {
        public string Name = string.Empty;
        public SourceReference SourceReference = new SourceReference();
    }

    public class Import
    {
        public string Path = string.Empty;
        public SourceReference SourceReference = new SourceReference();
    }

    public class SchemaFile
    {
        public string CanonicalPath = string.Empty;
        public ImmutableArray<ComponentDefinition> Components = ImmutableArray<ComponentDefinition>.Empty;
        public ImmutableArray<EnumDefinition> Enums = ImmutableArray<EnumDefinition>.Empty;
        public ImmutableArray<Import> Imports = ImmutableArray<Import>.Empty;
        public Package Package = new Package();
        public ImmutableArray<TypeDefinition> Types = ImmutableArray<TypeDefinition>.Empty;
    }
}
