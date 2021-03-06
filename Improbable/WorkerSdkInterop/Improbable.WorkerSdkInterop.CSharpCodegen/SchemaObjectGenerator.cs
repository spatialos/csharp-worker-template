using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Improbable.CSharpCodeGen;
using Improbable.Schema.Bundle;
using static Improbable.CSharpCodeGen.Case;
using static Improbable.CSharpCodeGen.Types;
using FieldType = Improbable.Schema.Bundle.FieldType;
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
            var typeName = type.TypeName();

            var content = new StringBuilder();
            var commandTypes = bundle.CommandTypes;

            content.AppendLine(GenerateSchemaConstructor(type, type.Fields).TrimEnd());
            content.AppendLine(GenerateApplyToSchemaObject(type.Fields).TrimEnd());
            content.AppendLine(GenerateUpdaters(type.Fields).TrimEnd());
            content.AppendLine(GenerateConstructor(type, type.Fields).TrimEnd());

            if (type.ComponentId.HasValue)
            {
                content.AppendLine(GenerateFromUpdate(type).TrimEnd());
                content.AppendLine(GenerateCreateGetEvents(type.Events).TrimEnd());

                if (!type.IsRestricted)
                {
                    // Workers can't construct or send updates for restricted components.
                    content.AppendLine(GenerateUpdateStruct(type, type.Fields).TrimEnd());
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
            var typeName = type.TypeName();

            var text = new StringBuilder();
            var sb = new StringBuilder();

            foreach (var field in fields)
            {
                var output = GetAssignmentForField(type, field, field.PascalCase());
                sb.AppendLine(output);
            }

            text.AppendLine($@"
internal {typeName}(global::Improbable.Worker.CInterop.SchemaObject fields)
{{
{Indent(1, sb.ToString().TrimEnd())}
}}");

            return text.ToString();
        }

        private static string GetApplyMapObject(TypeReference type, string mapObjectFieldId)
        {
            var value = mapObjectFieldId switch
            {
                SchemaMapKeyFieldId => "kv.Key",
                SchemaMapValueFieldId => "kv.Value",
                _ => throw new ArgumentException(nameof(mapObjectFieldId))
            };

            return type.ValueTypeSelector switch
            {
                ValueType.Enum => $"kvPair.AddEnum({mapObjectFieldId}, (uint) {value});",
                ValueType.Primitive when type.Primitive == PrimitiveType.EntityId => $"kvPair.Add{type.Primitive}({mapObjectFieldId}, {value}.Value);",
                ValueType.Primitive => $"kvPair.Add{type.Primitive}({mapObjectFieldId}, {value});",
                ValueType.Type => $"{value}.ApplyToSchemaObject(kvPair.AddObject({mapObjectFieldId}));",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static string GetApplyToSchemaObjectValueStatement(FieldDefinition field, string variableName)
        {
            var fieldAddMethod = GetFieldAddMethod(field);

            if (field.HasCustomType())
            {
                var value = field.IsOption() ? ".Value" : string.Empty;
                return $"{variableName}{value}.ApplyToSchemaObject(fields.{fieldAddMethod}({field.FieldId}));";
            }

            return $"fields.{fieldAddMethod}({field.FieldId}, {GetValueAccessor(field, variableName)});";
        }

        private static string GenerateApplyToSchemaObject(IEnumerable<FieldDefinition> fields)
        {
            var update = new StringBuilder();

            foreach (var field in fields)
            {
                var fieldName = field.PascalCase();

                var output = field.TypeSelector switch
                {
                    FieldType.Option => $"if ({fieldName}{field.OptionValueTest()}) {{ {GetApplyToSchemaObjectValueStatement(field, fieldName)} }}",

                    FieldType.List => $@"foreach(var value in {fieldName})
{{
        {GetApplyToSchemaObjectValueStatement(field, "value")}
}}",

#nullable disable
                    FieldType.Map => $@"foreach(var kv in {fieldName})
{{
        var kvPair = fields.AddObject({field.FieldId});
        {GetApplyMapObject(field.MapType.KeyType, SchemaMapKeyFieldId)}
        {GetApplyMapObject(field.MapType.ValueType, SchemaMapValueFieldId)}
}}",
#nullable restore
                    FieldType.Singular => $"{GetApplyToSchemaObjectValueStatement(field, fieldName)}",
                    _ => throw new ArgumentOutOfRangeException()
                };

                update.AppendLine(output);
            }

            return $@"
internal void ApplyToSchemaObject(global::Improbable.Worker.CInterop.SchemaObject fields)
{{
{Indent(1, update.ToString().TrimEnd())}
}}";
        }


        private static string GetMapObjectAssignment(TypeReference type, string fieldId)
        {
            var pairValue = $"kvPair.{GetTypeReferenceGetter(type)}({fieldId})";

            return type.ValueTypeSelector switch
            {
                // (EnumType) (value)
                ValueType.Enum => $"({type.Fqn()}) {pairValue}",
                // value
                ValueType.Primitive => pairValue,
                // new Type(value)
                ValueType.Type => $"new {type.Fqn()}({pairValue})",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static string GetAssignmentInstantiation(FieldDefinition field)
        {
            var prefix = "";
            var suffix = "";
            var indexer = "";

            var containedType = field.InnerFqn();
            var fieldAccessor = field.IsList() ? GetFieldIndexMethod(field) : GetFieldGetMethod(field);

            if (field.HasEnum())
            {
                prefix = $"({containedType}) ";
            }
            else if (field.HasPrimitive(PrimitiveType.Bytes))
            {
                prefix = $"({containedType}) ";
                suffix = ".Clone()";
            }
            else if (field.HasCustomType() || field.HasPrimitive(PrimitiveType.EntityId))
            {
                prefix = $"new {containedType}(";
                suffix = ")";
            }

            if (field.IsList())
            {
                indexer = ", i";
            }

            return $"{prefix}fields.{fieldAccessor}({field.FieldId}{indexer}){suffix}";
        }

        private static string GetContainerAddStatement(TypeDescription type, FieldDefinition field, string fieldName, string value)
        {
            if (IsFieldRecursive(type, field))
            {
                return $"local.Add({value});";
            }

            return $"{fieldName} = {fieldName}.Add({value});";
        }

        private static string GetAssignmentForField(TypeDescription type, FieldDefinition field, string fieldName)
        {
            var fieldCount = GetFieldCountMethod(field);
            var fieldIndexer = GetFieldIndexMethod(field);

            return field.TypeSelector switch
            {
                FieldType.Option => $@"if (fields.GetObjectCount({field.FieldId}) > 0)
{{
    {fieldName} = {GetAssignmentInstantiation(field)};
}}
else
{{
    {fieldName} = null;
}}",

                FieldType.List => $@"{{
    var count = fields.{fieldCount}({field.FieldId});
    var local = {GetEmptyCollection(type, field)};
    {fieldName} = local;

    for (uint i = 0; i < count; i++)
    {{
        {GetContainerAddStatement(type, field, fieldName, GetAssignmentInstantiation(field))}
    }}
}}",
#nullable disable
                FieldType.Map => $@"{{
    var count = fields.{fieldCount}({field.FieldId});
        var local = {GetEmptyCollection(type, field)};
    {fieldName} = local;

    for(uint i = 0; i < fields.{fieldCount}({field.FieldId}); i++)
    {{
        var kvPair = fields.{fieldIndexer}({field.FieldId}, i);
        {GetContainerAddStatement(type, field, fieldName, $"{GetMapObjectAssignment(field.MapType.KeyType, SchemaMapKeyFieldId)}, {GetMapObjectAssignment(field.MapType.ValueType, SchemaMapValueFieldId)}")};
    }}
}}",
#nullable restore
                FieldType.Singular => $"{fieldName} = {GetAssignmentInstantiation(field)};",

                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static string GenerateFromUpdate(TypeDescription type)
        {
            var typeName = type.TypeName();

            var text = new StringBuilder();
            var sb = new StringBuilder();
            foreach (var field in type.Fields)
            {
                var fieldName = field.PascalCase();
                var getCount = GetFieldCountMethod(field);

                var fieldUpdateExists = $"fields.{getCount}({field.FieldId}) > 0";

                var assignment = GetAssignmentForField(type, field, fieldName);

                var guard = field.TypeSelector switch
                {
                    FieldType.Singular => $@"if ({fieldUpdateExists})
{{
{Indent(1, assignment)}
}}",
                    // option, list, map
                    _ => $@"if ({fieldUpdateExists})
{{
{Indent(1, assignment)}
}}
else if (FieldIsCleared({field.FieldId}, clearedFields))
{{
    {fieldName} = {GetEmptyFieldInstantiationAsCsharp(type, field)};
}}"
                };

                sb.AppendLine(guard);
            }

            text.AppendLine($@"internal {typeName}(in {typeName} source, {SchemaComponentUpdate} update)
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

internal static {typeName} ApplyUpdate(in {typeName} source, {SchemaComponentUpdate}? update)
{{
    return update.HasValue ? new {typeName}(source, update.Value) : source;
}}

public global::Improbable.Worker.CInterop.ComponentData ToData()
{{
    var schemaData = global::Improbable.Worker.CInterop.SchemaComponentData.Create();
    ApplyToSchemaObject(schemaData.GetFields());

    return new global::Improbable.Worker.CInterop.ComponentData(ComponentId, schemaData);
}}");

            return text.ToString();
        }


        private static string GetValueAccessor(FieldDefinition field, string variableName) =>
            field.TypeSelector switch
            {
                FieldType.Option when field.HasEnum() => $"(uint) {variableName}.Value",
                FieldType.Option when field.CanPrimitiveBeNull() => variableName,
                FieldType.Option when field.HasPrimitive(PrimitiveType.EntityId) => $"{variableName}.Value.Value",
                FieldType.Option when field.HasCustomType() => $"{variableName}.Value",
                FieldType.Option => $"{variableName}.Value",

                FieldType.List when field.HasEnum() => $"(uint) {variableName}",
                FieldType.List when field.HasPrimitive(PrimitiveType.EntityId) => $"{variableName}.Value",
                FieldType.List => variableName,

                FieldType.Singular when field.HasEnum() => $"(uint) {variableName}",
                FieldType.Singular when field.HasPrimitive(PrimitiveType.EntityId) => $"{variableName}.Value",
                FieldType.Singular => variableName,

                _ => throw new ArgumentOutOfRangeException()
            };

        private static string GetUpdateValueStatement(FieldDefinition field, string variableName)
        {
            var fieldAddMethod = GetFieldAddMethod(field);

            if (field.HasCustomType())
            {
                var value = field.IsOption() ? ".Value" : string.Empty;
                return $"{variableName}{value}.ApplyToSchemaObject(fields.{fieldAddMethod}({field.FieldId}));";
            }

            return $"fields.{fieldAddMethod}({field.FieldId}, {GetValueAccessor(field, variableName)});";
        }

        private static string GenerateUpdaters(IEnumerable<FieldDefinition> fields)
        {
            var text = new StringBuilder();
            foreach (var field in fields)
            {
                var output = field.TypeSelector switch
                {
                    // =======
                    FieldType.Option => $@"if (newValue{field.OptionValueTest()})
{{
    {GetUpdateValueStatement(field, "newValue")}
}}
else
{{
    update.AddClearedField({field.FieldId});
}}",

                    // =======
                    FieldType.List => $@"var any = false;
if (newValue != null)
{{
    foreach(var value in newValue)
    {{
        {GetUpdateValueStatement(field, "value")}
        any = true;
    }}
}}

if (!any)
{{
    update.AddClearedField({field.FieldId});
}}",

                    // =======
#nullable disable
                    FieldType.Map => $@"var any = false;
if (newValue != null)
{{
    foreach(var kv in newValue)
    {{
        any = true;
        var kvPair = fields.AddObject({field.FieldId});
        {GetApplyMapObject(field.MapType.KeyType, SchemaMapKeyFieldId)}
        {GetApplyMapObject(field.MapType.ValueType, SchemaMapValueFieldId)}
    }}
}}

if (!any)
{{
    update.AddClearedField({field.FieldId});
}}",
#nullable restore

                    // =======
                    FieldType.Singular => GetUpdateValueStatement(field, "newValue"),

                    _ => throw new ArgumentOutOfRangeException()
                };

                text.AppendLine($@"
internal static void Update{field.PascalCase()}({SchemaComponentUpdate} update, {field.ParameterType()} newValue)
{{
    var fields = update.GetFields();
{Indent(1, output.TrimEnd())}
}}");
            }

            return text.ToString();
        }

        private static string GenerateCreateGetEvents(IReadOnlyCollection<ComponentDefinition.EventDefinition> events)
        {
            if (events.Count == 0)
            {
                return string.Empty;
            }

            var parameters = new StringBuilder();
            var eventGetters = new StringBuilder();
            foreach (var evt in events)
            {
                var eventPayloadType = evt.Fqn();
                var identifierName = evt.CamelCase();
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
    return {string.Join($"{Indent(2, "&& ")}\n", events.Select(evt => $"!{evt.CamelCase()}.IsDefaultOrEmpty"))};
}}";
        }

        private static string GenerateUpdateStruct(TypeDescription type, IReadOnlyList<FieldDefinition> fields)
        {
            var fieldText = new StringBuilder();

            var typeName = type.Fqn();

            foreach (var field in fields)
            {
                fieldText.AppendLine($"private {FqnFieldType(type, field)} {field.CamelCase()};");
                fieldText.AppendLine($"private bool was{field.PascalCase()}Updated;");
            }

            foreach (var ev in type.Events)
            {
                fieldText.AppendLine($"private global::System.Collections.Generic.List<{ev.Fqn()}> {ev.CamelCase()}Events;");
            }

            var setMethodText = new StringBuilder();

            foreach (var field in fields)
            {
                setMethodText.AppendLine($@"public Update Set{field.PascalCase()}({field.ParameterType()} {field.CamelCase()})
{{
    this.{field.CamelCase()} = {InitializeFromParameter(type, field)};
    was{field.PascalCase()}Updated = true;
    return this;
}}
");
            }

            foreach (var ev in type.Events)
            {
                setMethodText.AppendLine($@"public Update Add{SnakeCaseToPascalCase(ev.Name)}Event({ev.Fqn()} ev)
{{
    this.{ev.CamelCase()}Events = this.{ev.CamelCase()}Events ?? new global::System.Collections.Generic.List<{ev.Fqn()}>();
    this.{ev.CamelCase()}Events.Add(ev);
    return this;
}}
");
            }

            var toUpdateMethodBody = new StringBuilder();

            foreach (var field in fields)
            {
                var name = field.PascalCase();
                toUpdateMethodBody.AppendLine($@"if (was{name}Updated)
{{
    {typeName}.Update{name}(update, {field.CamelCase()});
}}
");
            }

            foreach (var ev in type.Events)
            {
                toUpdateMethodBody.AppendLine($@"if (this.{ev.CamelCase()}Events != null)
{{
    var events = update.GetEvents();

    foreach (var ev in this.{ev.CamelCase()}Events)
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
}}";
        }

        private static string GenerateConstructor(TypeDescription type, IReadOnlyCollection<FieldDefinition> fields)
        {
            var typeName = type.TypeName();

            var parameters = new StringBuilder();
            var initializers = new StringBuilder();
            var text = new StringBuilder();

            parameters.Append(string.Join(", ", fields.Select(f => $"{f.ParameterType()} {f.CamelCase()} = default")));
            initializers.AppendLine(string.Join(Environment.NewLine, fields.Select(f =>
            {
                var name = SnakeCaseToPascalCase(f.Name);

                // Allow `null` to represent an empty collection.
                if (f.IsList() || f.IsMap())
                {
                    return $"{name} = {f.CamelCase()} == null ? {GetEmptyCollection(type, f)} : {InitializeFromParameter(type, f)};";
                }

                return $"{name} = {InitializeFromParameter(type, f)};";
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
