using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.Serialization;

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

    public struct TypeReference
    {
        public string Enum;
        public PrimitiveType Primitive;
        public string Type;

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

    public struct Value
    {
        public bool BoolValue;
        public string BytesValue;
        public double DoubleValue;
        public long EntityIdValue;
        public SchemaEnumValue EnumValue;
        public float FloatValue;
        public int Int32Value;
        public long Int64Value;
        public ListValueHolder ListValue;
        public MapValueHolder MapValue;
        public OptionValueHolder OptionValue;
        public SourceReference SourceReference;
        public string StringValue;
        public TypeValue TypeValue;
        public uint Uint32Value;
        public ulong Uint64Value;

        public class OptionValueHolder
        {
            public Value Value;
        }

        public struct ListValueHolder
        {
            public ImmutableList<Value> Values;

            [OnSerialized]
            private void OnSerialized(StreamingContext context)
            {
                Values ??= ImmutableList<Value>.Empty;
            }
        }

        public struct MapValueHolder
        {
            public ImmutableArray<MapPairValue> Values;

            public class MapPairValue
            {
                public Value Key;
                public Value Value;
            }
        }
    }

    public struct SchemaEnumValue
    {
        public string Enum;
        public string EnumValue;

        public string Name;
        public string Value;
    }

    public struct TypeValue
    {
        public ImmutableList<FieldValue> Fields;

        public string Type;

        [OnSerialized]
        private void OnSerialized(StreamingContext context)
        {
            Fields ??= ImmutableList<FieldValue>.Empty;
            Type ??= "";
        }

        public struct FieldValue
        {
            public string Name;
            public SourceReference SourceReference;
            public Value Value;
        }
    }

    public struct Annotation
    {
        public SourceReference SourceReference;
        public TypeValue TypeValue;
    }

    public struct EnumValueDefinition
    {
        public ImmutableArray<Annotation> Annotations;
        public string Name;
        public SourceReference SourceReference;

        public uint Value;
    }

    [DebuggerDisplay("{" + nameof(QualifiedName) + "}")]
    public struct EnumDefinition
    {
        public ImmutableArray<Annotation> Annotations;
        public string Name;
        public string OuterType;
        public string QualifiedName;
        public SourceReference SourceReference;
        public ImmutableArray<EnumValueDefinition> Values;
    }

    public enum FieldType
    {
        Option,
        List,
        Map,
        Singular
    }

    [DebuggerDisplay("{" + nameof(Name) + "}" + " ({" + nameof(FieldId) + "})")]
    public struct FieldDefinition
    {
        public ImmutableArray<Annotation> Annotations;

        public string Name;
        public uint FieldId;

        public ListTypeRef ListType;
        public MapTypeRef MapType;
        public OptionTypeRef OptionType;
        public SingularTypeRef SingularType;

        public SourceReference SourceReference;

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
            public TypeReference Type;
        }

        public class OptionTypeRef
        {
            public TypeReference InnerType;
        }

        public class ListTypeRef
        {
            public TypeReference InnerType;
        }

        public class MapTypeRef
        {
            public TypeReference KeyType;
            public TypeReference ValueType;
        }
    }

    [DebuggerDisplay("{" + nameof(QualifiedName) + "}")]
    public struct TypeDefinition
    {
        public ImmutableArray<Annotation> Annotations;
        public ImmutableArray<FieldDefinition> Fields;
        public string Name;
        public string OuterType;
        public string QualifiedName;
        public SourceReference SourceReference;
    }

    [DebuggerDisplay("{" + nameof(QualifiedName) + "} {" + nameof(ComponentId) + "}")]
    public struct ComponentDefinition
    {
        public ImmutableArray<Annotation> Annotations;
        public ImmutableArray<CommandDefinition> Commands;
        public uint ComponentId;

        public string DataDefinition;
        public ImmutableArray<EventDefinition> Events;

        public ImmutableArray<FieldDefinition> Fields;
        public string Name;

        public string QualifiedName;
        public SourceReference SourceReference;

        [DebuggerDisplay("{" + nameof(QualifiedName) + "}")]
        public struct EventDefinition
        {
            public ImmutableArray<Annotation> Annotations;
            public uint EventIndex;
            public string Name;
            public SourceReference SourceReference;
            public string Type;
        }

        [DebuggerDisplay("{" + nameof(QualifiedName) + "}" + " {" + nameof(CommandIndex) + "}")]
        public struct CommandDefinition
        {
            public ImmutableArray<Annotation> Annotations;
            public uint CommandIndex;

            public string Name;
            public string RequestType;
            public string ResponseType;
            public SourceReference SourceReference;
        }
    }

    public struct SourceReference
    {
        public uint Column;
        public uint Line;
    }

    public struct SchemaBundle
    {
        public ImmutableArray<SchemaFile> SchemaFiles;
    }

    public struct Package
    {
        public string Name;
        public SourceReference SourceReference;
    }

    public struct Import
    {
        public string Path;
        public SourceReference SourceReference;
    }

    public struct SchemaFile
    {
        public string CanonicalPath;
        public ImmutableArray<ComponentDefinition> Components;
        public ImmutableArray<EnumDefinition> Enums;
        public ImmutableArray<Import> Imports;
        public Package Package;
        public ImmutableArray<TypeDefinition> Types;
    }
}
