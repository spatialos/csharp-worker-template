using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Improbable.CSharpCodeGen;
using Improbable.Schema.Bundle;
using static Improbable.CSharpCodeGen.Case;
using static Improbable.CSharpCodeGen.Types;
using ValueType = Improbable.Schema.Bundle.ValueType;

namespace Improbable.WorkerSdkInterop.CSharpCodeGen
{
    public class SchemaObjectGenerator : ICodeGenerator
    {
        private readonly Bundle bundle;

        public SchemaObjectGenerator(Bundle bundle)
        {
            this.bundle = bundle;
        }

        public string Generate(TypeDescription type)
        {
            var typeName = GetPascalCaseNameFromTypeName(type.QualifiedName);

            var content = new StringBuilder();
            var commandTypes = bundle.CommandTypes;

            var filteredFields = type.Fields.Where(f => !IsFieldTypeRecursive(bundle, type.QualifiedName, f)).ToList();

            content.AppendLine(GenerateSchemaConstructor(type, filteredFields).TrimEnd());
            content.AppendLine(GenerateApplyToSchemaObject(filteredFields).TrimEnd());
            content.AppendLine(GenerateUpdaters(filteredFields).TrimEnd());
            content.AppendLine(GenerateConstructor(type, filteredFields));

            if (type.ComponentId.HasValue)
            {
                content.AppendLine(GenerateFromUpdate(type, type.ComponentId.Value).TrimEnd());
                content.AppendLine(GenerateCreateGetEvents(type.Events).TrimEnd());

                if (!type.IsRestricted)
                {
                    // Workers can't construct or send updates for restricted components.
                    content.AppendLine(GenerateUpdateStruct(type, filteredFields));
                }
            }

            if (commandTypes.Contains(type.QualifiedName))
            {
                content.AppendLine($@"public static {typeName} Create(global::Improbable.Worker.CInterop.SchemaCommandRequest? fields)
{{
    return fields.HasValue ? new {typeName}(fields.Value.GetObject()) : new {typeName}();
}}");
            }

            return content.ToString();
        }

        private static string GenerateSchemaConstructor(TypeDescription type, IEnumerable<FieldDefinition> fields)
        {
            var typeName = GetPascalCaseNameFromTypeName(type.QualifiedName);

            var text = new StringBuilder();
            var sb = new StringBuilder();

            foreach (var field in fields)
            {
                var fieldName = SnakeCaseToPascalCase(field.Name);

                var output = GetAssignmentForField(type, field, fieldName);
                sb.AppendLine(output);
            }

            text.AppendLine($@"
internal {typeName}(global::Improbable.Worker.CInterop.SchemaObject fields)
{{
{Indent(1, sb.ToString().TrimEnd())}
}}");

            return text.ToString();
        }

        private static string GenerateApplyToSchemaObject(IEnumerable<FieldDefinition> fields)
        {
            var update = new StringBuilder();

            foreach (var field in fields)
            {
                var fieldName = SnakeCaseToPascalCase(field.Name);
                var fieldAddMethod = GetFieldAddMethod(field);

                string output;
                switch (field.TypeSelector)
                {
                    case FieldType.Option:
                        output = field.OptionType.InnerType.ValueTypeSelector switch
                        {
                            ValueType.Enum => $"if ({fieldName}.HasValue) {{ fields.{fieldAddMethod}({field.FieldId}, (uint){fieldName}.Value); }}",
                            ValueType.Primitive => (field.OptionType.InnerType.Primitive switch
                            {
                                PrimitiveType.Bytes => $"if ({fieldName} != null ) {{ fields.{fieldAddMethod}({field.FieldId}, {fieldName}); }}",
                                PrimitiveType.String => $"if ({fieldName} != null ) {{ fields.{fieldAddMethod}({field.FieldId}, {fieldName}); }}",
                                PrimitiveType.EntityId => $"if ({fieldName}.HasValue) {{ fields.{fieldAddMethod}({field.FieldId}, {fieldName}.Value.Value); }}",
                                _ => $"if ({fieldName}.HasValue) {{ fields.{fieldAddMethod}({field.FieldId}, {fieldName}.Value); }}"
                            }),
                            ValueType.Type => $"if ({fieldName}.HasValue) {{ {fieldName}.Value.ApplyToSchemaObject(fields.{fieldAddMethod}({field.FieldId})); }}",
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        break;
                    case FieldType.List:
                        var addType = field.ListType.InnerType.ValueTypeSelector switch
                        {
                            ValueType.Enum => $"fields.AddEnum({field.FieldId}, (uint)value);",
                            ValueType.Primitive => (field.ListType.InnerType.Primitive switch
                            {
                                PrimitiveType.EntityId => $"fields.Add{field.ListType.InnerType.Primitive}({field.FieldId}, value.Value);",
                                _ => $"fields.Add{field.ListType.InnerType.Primitive}({field.FieldId}, value);"
                            }),
                            ValueType.Type => $"value.ApplyToSchemaObject(fields.AddObject({field.FieldId}));",
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        output =
                            $@"if ({fieldName} != null)
{{
    foreach(var value in {fieldName})
    {{
        {addType}
    }}
}}";

                        break;
                    case FieldType.Map:
                        string setKeyType;
                        string setValueType;

                        setKeyType = field.MapType.KeyType.ValueTypeSelector switch
                        {
                            ValueType.Enum => $"kvPair.AddEnum({SchemaMapKeyFieldId}, (uint)kv.Key);",
                            ValueType.Primitive => (field.MapType.KeyType.Primitive switch
                            {
                                PrimitiveType.EntityId => $"kvPair.Add{field.MapType.KeyType.Primitive}({SchemaMapKeyFieldId}, kv.Key.Value);",
                                _ => $"kvPair.Add{field.MapType.KeyType.Primitive}({SchemaMapKeyFieldId}, kv.Key);"
                            }),
                            ValueType.Type => $"kv.Key.ApplyToSchemaObject(kvPair.AddObject({SchemaMapKeyFieldId}));",
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        setValueType = field.MapType.ValueType.ValueTypeSelector switch
                        {
                            ValueType.Enum => $"kvPair.AddEnum({SchemaMapValueFieldId}, (uint)kv.Value);",
                            ValueType.Primitive => (field.MapType.ValueType.Primitive switch
                            {
                                PrimitiveType.EntityId => $"kvPair.Add{field.MapType.ValueType.Primitive}({SchemaMapValueFieldId}, kv.Value.Value);",
                                _ => $"kvPair.Add{field.MapType.ValueType.Primitive}({SchemaMapValueFieldId}, kv.Value);"
                            }),
                            ValueType.Type => $"kv.Value.ApplyToSchemaObject(kvPair.AddObject({SchemaMapValueFieldId}));",
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        output =
                            $@"if ({fieldName} != null)
{{
    foreach(var kv in {fieldName})
    {{
        var kvPair = fields.AddObject({field.FieldId});
        {setKeyType}
        {setValueType}
    }}
}}";
                        break;
                    case FieldType.Singular:
                        output = field.SingularType.Type.ValueTypeSelector switch
                        {
                            ValueType.Enum => $"fields.{fieldAddMethod}({field.FieldId}, (uint) {fieldName});",
                            ValueType.Primitive => (field.SingularType.Type.Primitive switch
                            {
                                PrimitiveType.EntityId => $"fields.{fieldAddMethod}({field.FieldId}, {fieldName}.Value);",
                                _ => $"fields.{fieldAddMethod}({field.FieldId}, {fieldName});"
                            }),
                            ValueType.Type => $"{fieldName}.ApplyToSchemaObject(fields.{fieldAddMethod}({field.FieldId}));",
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                update.AppendLine(output);
            }

            return $@"
internal void ApplyToSchemaObject(global::Improbable.Worker.CInterop.SchemaObject fields)
{{
{Indent(1, update.ToString().TrimEnd())}
}}";
        }

        private static string GetAssignmentForField(TypeDescription type, FieldDefinition field, string fieldName)
        {
            var fieldAccessor = GetFieldGetMethod(field);
            var fieldCount = GetFieldCountMethod(field);
            string output;

            switch (field.TypeSelector)
            {
                case FieldType.Option:
                    switch (field.OptionType.InnerType.ValueTypeSelector)
                    {
                        case ValueType.Enum:
                            output = $"{fieldName} = ({CapitalizeNamespace(field.OptionType.InnerType.Enum)})fields.{fieldAccessor}({field.FieldId});";
                            break;
                        case ValueType.Primitive:
                            output = $"{fieldName} = fields.{fieldAccessor}({field.FieldId});";
                            break;
                        case ValueType.Type:
                            var objectType = CapitalizeNamespace(field.OptionType.InnerType.Type);
                            output =
                                $@"{fieldName} = new {objectType}(fields.{fieldAccessor}({field.FieldId}));";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    output = $@"if (fields.GetObjectCount({field.FieldId}) > 0)
{{
{Indent(1, output)}
}}
else
{{
    {fieldName} = null;
}}";
                    break;
                case FieldType.List:
                    switch (field.ListType.InnerType.ValueTypeSelector)
                    {
                        case ValueType.Enum:
                            var name = CapitalizeNamespace(field.ListType.InnerType.Enum);
                            output =
                                $@"{{
    var elements = fields.{fieldAccessor}({field.FieldId});
    {fieldName} = global::System.Collections.Immutable.ImmutableArray<{name}>.Empty;
    for (uint i = 0; i < elements.Length; i++)
    {{
        {fieldName} = {fieldName}.Add(({name}) elements[i]);
    }}
}}";
                            break;
                        case ValueType.Primitive:
                            output = field.ListType.InnerType.Primitive switch
                            {
                                PrimitiveType.Bytes => $@"{{
    var count = fields.GetBytesCount({field.FieldId});
    {fieldName} = global::System.Collections.Immutable.ImmutableArray<byte[]>.Empty;
    for (uint i = 0; i < count; i++)
    {{
        {fieldName} = {fieldName}.Add(fields.IndexBytes({field.FieldId}, i));
    }}
}}",
                                PrimitiveType.String => $@"{{
    var count = fields.GetStringCount({field.FieldId});
    {fieldName} = global::System.Collections.Immutable.ImmutableArray<string>.Empty;

    for (uint i = 0; i < count; i++)
    {{
        {fieldName} = {fieldName}.Add(fields.IndexString({field.FieldId}, i));
    }}
}}",
                                PrimitiveType.EntityId => $@"{{
    var count = fields.GetEntityIdCount({field.FieldId});
    {fieldName} = global::System.Collections.Immutable.ImmutableArray<{EntityIdType}>.Empty;

    for (uint i = 0; i < count; i++)
    {{
        {fieldName} = {fieldName}.Add(new {EntityIdType}(fields.IndexEntityId({field.FieldId}, i)));
    }}
}}",
                                _ => $"{fieldName} = {GetEmptyFieldInstantiationAsCsharp(type, field)}.AddRange(fields.{fieldAccessor}({field.FieldId}));"
                            };

                            break;
                        case ValueType.Type:
                            var typeName = CapitalizeNamespace(field.ListType.InnerType.Type);
                            if (IsFieldRecursive(type, field))
                            {
                                output =
                                    $@"var local{fieldName} = {GetEmptyFieldInstantiationAsCsharp(type, field)};
{fieldName} = local{fieldName};

for(uint i = 0; i < fields.{fieldCount}({field.FieldId}); i++)
{{
    local{fieldName}.Add(new {typeName}(fields.{fieldAccessor}({field.FieldId}, i)));
}}";
                            }
                            else
                            {
                                output =
                                    $@"
{fieldName} = {GetEmptyFieldInstantiationAsCsharp(type, field)};
for(uint i = 0; i < fields.{fieldCount}({field.FieldId}); i++)
{{
    {fieldName} = {fieldName}.Add(new {typeName}(fields.{fieldAccessor}({field.FieldId}, i)));
}}";
                            }

                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    break;
                case FieldType.Map:
                    string getKeyType;
                    string getValueType;

                    getKeyType = field.MapType.KeyType.ValueTypeSelector switch
                    {
                        ValueType.Enum => $"({CapitalizeNamespace(field.MapType.KeyType.Enum)}) kvPair.GetEnum({SchemaMapKeyFieldId})",
                        ValueType.Primitive => $"kvPair.Get{field.MapType.KeyType.Primitive}({SchemaMapKeyFieldId})",
                        ValueType.Type => $"new {CapitalizeNamespace(field.MapType.KeyType.Type)}(kvPair.GetObject({SchemaMapKeyFieldId}))",
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    getValueType = field.MapType.ValueType.ValueTypeSelector switch
                    {
                        ValueType.Enum => $"({CapitalizeNamespace(field.MapType.ValueType.Enum)}) kvPair.GetEnum({SchemaMapValueFieldId})",
                        ValueType.Primitive => $"kvPair.Get{field.MapType.ValueType.Primitive}({SchemaMapValueFieldId})",
                        ValueType.Type => $"new {CapitalizeNamespace(field.MapType.ValueType.Type)}(kvPair.GetObject({SchemaMapValueFieldId}))",
                        _ => throw new ArgumentOutOfRangeException()
                    };

                    if (IsFieldRecursive(type, field))
                    {
                        output =
                            $@"var local{fieldName} = {GetEmptyFieldInstantiationAsCsharp(type, field)};
{fieldName} = local{fieldName};

for(uint i = 0; i < fields.{fieldCount}({field.FieldId}); i++)
{{
    var kvPair = fields.IndexObject({field.FieldId}, i);
    local{fieldName}.Add({getKeyType}, {getValueType});
}}";
                    }
                    else
                    {
                        output =
                            $@"{fieldName} = {GetEmptyFieldInstantiationAsCsharp(type, field)};

for(uint i = 0; i < fields.{fieldCount}({field.FieldId}); i++)
{{
    var kvPair = fields.IndexObject({field.FieldId}, i);
    {fieldName} = {fieldName}.Add({getKeyType}, {getValueType});
}}";
                    }

                    break;
                case FieldType.Singular:
                    switch (field.SingularType.Type.ValueTypeSelector)
                    {
                        case ValueType.Enum:
                            output = $"{fieldName} = ({CapitalizeNamespace(field.SingularType.Type.Enum)})fields.{fieldAccessor}({field.FieldId});";
                            break;
                        case ValueType.Primitive:
                            output = field.SingularType.Type.Primitive switch
                            {
                                PrimitiveType.Bytes => $"{fieldName} = ({SchemaToCSharpTypes[field.SingularType.Type.Primitive]}) fields.{fieldAccessor}({field.FieldId}).Clone();",
                                _ => $"{fieldName} = fields.{fieldAccessor}({field.FieldId});"
                            };

                            break;
                        case ValueType.Type:
                            var objectType = CapitalizeNamespace(field.SingularType.Type.Type);
                            output = $"{fieldName} = new {objectType}(fields.{fieldAccessor}({field.FieldId}));";
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return output;
        }

        private static string GenerateFromUpdate(TypeDescription type, uint componentId)
        {
            var typeName = GetPascalCaseNameFromTypeName(type.QualifiedName);

            var text = new StringBuilder();
            var sb = new StringBuilder();
            foreach (var field in type.Fields)
            {
                var fieldName = $"{SnakeCaseToPascalCase(field.Name)}";

                var output = GetAssignmentForField(type, field, fieldName);
                var fieldCount = GetFieldCountMethod(field);

                var guard = field.TypeSelector switch
                {
                    FieldType.Option => $@"if (fields.{fieldCount}({field.FieldId}) > 0)
{{
{Indent(1, output)}
}}
else if (FieldIsCleared({field.FieldId}, clearedFields))
{{
    {fieldName} = {GetEmptyFieldInstantiationAsCsharp(type, field)};
}}",

                    FieldType.List => $@"if (fields.{fieldCount}({field.FieldId}) > 0)
{{
{Indent(1, output)}
}}
else if (FieldIsCleared({field.FieldId}, clearedFields))
{{
    {fieldName} = {GetEmptyFieldInstantiationAsCsharp(type, field)};
}}",

                    FieldType.Map => $@"if (fields.{fieldCount}({field.FieldId}) > 0)
{{
{Indent(1, output)}
}}
else if (FieldIsCleared({field.FieldId}, clearedFields))
{{
    {fieldName} = {GetEmptyFieldInstantiationAsCsharp(type, field)};
}}",

                    FieldType.Singular => $@"if (fields.{fieldCount}({field.FieldId}) > 0)
{{
{Indent(1, output)}
}}",
                    _ => throw new ArgumentOutOfRangeException()
                };

                sb.AppendLine(guard);
            }

            text.AppendLine($@"internal {typeName}({typeName} source, {SchemaComponentUpdate} update)
{{
    var fields = update.GetFields();
    var clearedFields = update.GetClearedFields();
    this = source;

{Indent(1, sb.ToString().TrimEnd())}
}}");

            text.AppendLine(
                $@"private static bool FieldIsCleared(uint fieldId, uint[] clearFields)
{{
    for (var i = 0; i < clearFields.Length; i++)
    {{
        if (clearFields[i] == fieldId)
        {{
            return true;
        }}
    }}

    return false;
}}

public static {typeName} Create(global::Improbable.Worker.CInterop.SchemaComponentData? fields)
{{
    return fields.HasValue ? new {typeName}(fields.Value.GetFields()) : new {typeName}();
}}

public static {typeName} CreateFromSnapshot(global::Improbable.Worker.CInterop.Entity snapshotEntity)
{{
    var component = snapshotEntity.Get(ComponentId);
    if (component.HasValue)
    {{
        return Create(component.Value.SchemaData);
    }}
    return new {typeName}();
}}

internal static {typeName} ApplyUpdate({typeName} source, {SchemaComponentUpdate}? update)
{{
    return update.HasValue ? new {typeName}(source, update.Value) : source;
}}

public global::Improbable.Worker.CInterop.ComponentData ToData()
{{
    var schemaData = global::Improbable.Worker.CInterop.SchemaComponentData.Create();
    ApplyToSchemaObject(schemaData.GetFields());

    return new global::Improbable.Worker.CInterop.ComponentData({componentId}, schemaData);
}}");

            return text.ToString();
        }

        private static string GenerateUpdaters(IEnumerable<FieldDefinition> fields)
        {
            var text = new StringBuilder();
            foreach (var field in fields)
            {
                var fieldAddMethod = GetFieldAddMethod(field);

                string output;
                switch (field.TypeSelector)
                {
                    case FieldType.Option:
                        output = field.OptionType.InnerType.ValueTypeSelector switch
                        {
                            ValueType.Enum => $@"if (newValue.HasValue)
{{
    fields.{fieldAddMethod}({field.FieldId}, (uint) newValue.Value);
}}
else
{{
    update.AddClearedField({field.FieldId});
}}",
                            ValueType.Primitive => (field.OptionType.InnerType.Primitive switch
                            {
                                PrimitiveType.Bytes => $@"if (newValue != null)
{{
    fields.{fieldAddMethod}({field.FieldId}, newValue);
}}
else
{{
    update.AddClearedField({field.FieldId});
}}",
                                PrimitiveType.String => $@"if (newValue != null)
{{
    fields.{fieldAddMethod}({field.FieldId}, newValue);
}}
else
{{
    update.AddClearedField({field.FieldId});
}}",
                                PrimitiveType.EntityId => $@"if (newValue.HasValue)
{{
    fields.{fieldAddMethod}({field.FieldId}, newValue.Value.Value);
}}
else
{{
    update.AddClearedField({field.FieldId});
}}",
                                _ => $@"if (newValue.HasValue)
{{
    fields.{fieldAddMethod}({field.FieldId}, newValue.Value);
}}
else
{{
    update.AddClearedField({field.FieldId});
}}"
                            }),
                            ValueType.Type => $@"if (newValue.HasValue)
{{
    newValue.Value.ApplyToSchemaObject(fields.{fieldAddMethod}({field.FieldId}));
}}
else
{{
    update.AddClearedField({field.FieldId});
}}",
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        break;
                    case FieldType.List:
                        var addType = field.ListType.InnerType.ValueTypeSelector switch
                        {
                            ValueType.Enum => $"fields.AddEnum({field.FieldId}, (uint)value);",
                            ValueType.Primitive => (field.ListType.InnerType.Primitive switch
                            {
                                PrimitiveType.EntityId => $"fields.Add{field.ListType.InnerType.Primitive}({field.FieldId}, value.Value);",
                                _ => $"fields.Add{field.ListType.InnerType.Primitive}({field.FieldId}, value);"
                            }),
                            ValueType.Type => $"value.ApplyToSchemaObject(fields.AddObject({field.FieldId}));",
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        output =
                            $@"var any = false;
if (newValue != null)
{{
    foreach(var value in newValue)
    {{
        {addType};
        any = true;
    }}
}}

if (!any)
{{
    update.AddClearedField({field.FieldId});
}}";

                        break;
                    case FieldType.Map:
                        string setKeyType;
                        string setValueType;

                        setKeyType = field.MapType.KeyType.ValueTypeSelector switch
                        {
                            ValueType.Enum => $"kvPair.AddEnum({SchemaMapKeyFieldId}, (uint)kv.Key);",
                            ValueType.Primitive => (field.MapType.KeyType.Primitive switch
                            {
                                PrimitiveType.EntityId => $"kvPair.Add{field.MapType.KeyType.Primitive}({SchemaMapKeyFieldId}, kv.Key.Value);",
                                _ => $"kvPair.Add{field.MapType.KeyType.Primitive}({SchemaMapKeyFieldId}, kv.Key);"
                            }),
                            ValueType.Type => $"kv.Key.ApplyToSchemaObject(kvPair.AddObject({SchemaMapKeyFieldId}));",
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        setValueType = field.MapType.ValueType.ValueTypeSelector switch
                        {
                            ValueType.Enum => $"kvPair.AddEnum({SchemaMapValueFieldId}, (uint)kv.Value);",
                            ValueType.Primitive => (field.MapType.ValueType.Primitive switch
                            {
                                PrimitiveType.EntityId => $"kvPair.Add{field.MapType.ValueType.Primitive}({SchemaMapValueFieldId}, kv.Value.Value);",
                                _ => $"kvPair.Add{field.MapType.ValueType.Primitive}({SchemaMapValueFieldId}, kv.Value);"
                            }),
                            ValueType.Type => $"kv.Value.ApplyToSchemaObject(kvPair.AddObject({SchemaMapValueFieldId}));",
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        output =
                            $@"var any = false;
if (newValue != null)
{{
    foreach(var kv in newValue)
    {{
        any = true;
        var kvPair = fields.AddObject({field.FieldId});
        {setKeyType}
        {setValueType}
    }}
}}

if (!any)
{{
    update.AddClearedField({field.FieldId});
}}";
                        break;
                    case FieldType.Singular:
                        output = field.SingularType.Type.ValueTypeSelector switch
                        {
                            ValueType.Enum => $"fields.{fieldAddMethod}({field.FieldId}, (uint) newValue);",
                            ValueType.Primitive => (field.SingularType.Type.Primitive switch
                            {
                                PrimitiveType.EntityId => $"fields.{fieldAddMethod}({field.FieldId}, newValue.Value);",
                                _ => $"fields.{fieldAddMethod}({field.FieldId}, newValue);"
                            }),
                            ValueType.Type => $"newValue.ApplyToSchemaObject(fields.{fieldAddMethod}({field.FieldId}));",
                            _ => throw new ArgumentOutOfRangeException()
                        };

                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                text.AppendLine($@"
internal static void Update{SnakeCaseToPascalCase(field.Name)}({SchemaComponentUpdate} update, {GetParameterTypeAsCsharp(field)} newValue)
{{
    var fields = update.GetFields();
{Indent(1, output.TrimEnd())}
}}");
            }

            return text.ToString();
        }

        private static string GenerateCreateGetEvents(IReadOnlyCollection<ComponentDefinition.EventDefinition> events)
        {
            if (events == null || events.Count == 0)
            {
                return string.Empty;
            }

            var parameters = new StringBuilder();
            var eventGetters = new StringBuilder();
            foreach (var evt in events)
            {
                var eventPayloadType = CapitalizeNamespace(CapitalizeNamespace(evt.Type));
                var identifierName = FieldNameToSafeName(SnakeCaseToCamelCase(evt.Name));
                parameters.Append($",\n\t\tout global::System.Collections.Immutable.ImmutableArray<{eventPayloadType}> {identifierName}");

                eventGetters.Append($@"{identifierName} = global::System.Collections.Immutable.ImmutableArray<{eventPayloadType}>.Empty;
for (uint i = 0; i < events.GetObjectCount({evt.EventIndex}); i++)
{{
    {identifierName} = {identifierName}.Add(new {eventPayloadType}(events.IndexObject({evt.EventIndex}, i)));
}}
");
            }

            return $@"
public static bool TryGetEvents({SchemaComponentUpdate} update{parameters})
{{
    var events = update.GetEvents();

{Indent(1, eventGetters.ToString().TrimEnd())}
    return {string.Join($"{Indent(2, "&& ")}\n", events.Select(evt => $"!{FieldNameToSafeName(SnakeCaseToCamelCase(evt.Name))}.IsDefaultOrEmpty"))};
}}";
        }

        private static string GenerateUpdateStruct(TypeDescription type, IReadOnlyList<FieldDefinition> fields)
        {
            var fieldText = new StringBuilder();

            var typeName = GetPascalCaseNameFromTypeName(type.QualifiedName);
            var typeNamespace = GetPascalCaseNamespaceFromTypeName(type.QualifiedName);

            foreach (var field in fields)
            {
                fieldText.AppendLine($"private {GetFieldTypeAsCsharp(type, field)} {FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))};");
                fieldText.AppendLine($"private bool was{SnakeCaseToPascalCase(field.Name)}Updated;");
            }

            foreach (var ev in type.Events)
            {
                fieldText.AppendLine($"private global::System.Collections.Generic.List<global::{CapitalizeNamespace(ev.Type)}> {SnakeCaseToCamelCase(ev.Name)}Events;");
            }

            var setMethodText = new StringBuilder();

            foreach (var field in fields)
            {
                setMethodText.AppendLine($@"public Update Set{SnakeCaseToPascalCase(field.Name)}({GetParameterTypeAsCsharp(field)} {FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))})
{{
    this.{FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))} = {ParameterConversion(type, field)};
    was{SnakeCaseToPascalCase(field.Name)}Updated = true;
    return this;
}}
");
            }

            foreach (var ev in type.Events)
            {
                setMethodText.AppendLine($@"public Update Add{SnakeCaseToPascalCase(ev.Name)}Event(global::{CapitalizeNamespace(ev.Type)} ev)
{{
    this.{SnakeCaseToCamelCase(ev.Name)}Events = this.{SnakeCaseToCamelCase(ev.Name)}Events ?? new global::System.Collections.Generic.List<global::{CapitalizeNamespace(ev.Type)}>();
    this.{SnakeCaseToCamelCase(ev.Name)}Events.Add(ev);
    return this;
}}
");
            }

            var toUpdateMethodBody = new StringBuilder();

            foreach (var field in fields)
            {
                toUpdateMethodBody.AppendLine($@"if (was{SnakeCaseToPascalCase(field.Name)}Updated)
{{
    global::{typeNamespace}.{typeName}.Update{SnakeCaseToPascalCase(field.Name)}(update, {FieldNameToSafeName(SnakeCaseToCamelCase(field.Name))});
}}
");
            }

            foreach (var ev in type.Events)
            {
                toUpdateMethodBody.AppendLine($@"if (this.{SnakeCaseToCamelCase(ev.Name)}Events != null)
{{
    var events = update.GetEvents();

    foreach (var ev in this.{SnakeCaseToCamelCase(ev.Name)}Events)
    {{
        ev.ApplyToSchemaObject(events.AddObject({ev.EventIndex}));
    }}
}}");
            }

            return $@"public partial struct Update
{{
{Indent(1, fieldText.ToString().TrimEnd())}

{Indent(1, setMethodText.ToString().TrimEnd())}

    public {SchemaComponentUpdate} ToSchemaUpdate()
    {{
        var update = {SchemaComponentUpdate}.Create();

{Indent(2, toUpdateMethodBody.ToString().TrimEnd())}

        return update;
    }}
}}
";
        }

        private static string GenerateConstructor(TypeDescription type, IReadOnlyCollection<FieldDefinition> fields)
        {
            var typeName = GetPascalCaseNameFromTypeName(type.QualifiedName);

            var parameters = new StringBuilder();
            var initializers = new StringBuilder();
            var text = new StringBuilder();

            parameters.Append(string.Join(", ", fields.Select(f => $"{GetParameterTypeAsCsharp(f)} {FieldNameToSafeName(SnakeCaseToCamelCase(f.Name))} = default")));
            initializers.AppendLine(string.Join(Environment.NewLine, fields.Select(f =>
            {
                var name = SnakeCaseToPascalCase(f.Name);

                // Allow `null` to represent an empty collection.
                if (f.TypeSelector == FieldType.List || f.TypeSelector == FieldType.Map)
                {
                    return $"{name} = {FieldNameToSafeName(SnakeCaseToCamelCase(f.Name))} == null ? {EmptyCollection(type, f)} : {ParameterConversion(type, f)};";
                }

                return $"{name} = {ParameterConversion(type, f)};";
            })));

            if (fields.Count > 0)
            {
                text.Append($@"
public {typeName}({parameters})
{{
{Indent(1, initializers.ToString().TrimEnd())}
}}");
            }

            return text.ToString();
        }
    }
}
