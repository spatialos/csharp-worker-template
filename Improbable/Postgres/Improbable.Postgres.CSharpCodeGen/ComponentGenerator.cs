using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Improbable.CSharpCodeGen;
using Improbable.Schema.Bundle;
using static Improbable.CSharpCodeGen.Types;
using static Improbable.CSharpCodeGen.Case;
using FieldType = Improbable.Schema.Bundle.FieldType;

namespace Improbable.Postgres.CSharpCodeGen
{
    public class ComponentGenerator : ICodeGenerator
    {
        private readonly string tableName;

        public ComponentGenerator(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentNullException(nameof(tableName));
            }

            this.tableName = tableName;
        }

        private static string FormatFieldValue(FieldDefinition field)
        {
            var name = $"value.{field.PascalCase()}";

            return field.TypeSelector switch
            {
                FieldType.Option when field.HasPrimitive(PrimitiveType.String) => $@"{name}.HasValue ? '{{{name}.Value}}' : ""null""",
                FieldType.Option when field.HasEnum() => $@"{name}.HasValue ? (int) {{{name}.Value}} : ""null""",
                FieldType.Option => $@"{name}.HasValue ? '{{{name}.Value}}' : ""null""",

                FieldType.Singular when field.HasPrimitive(PrimitiveType.String) => $@"'{{{name}}}'",
                FieldType.Singular when field.HasEnum() => $@"(int) {{{name}}}",
                FieldType.Singular => $@"{{{name}}}",

                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public string Generate(TypeDescription type)
        {
            if (!HasAnnotation(type, WellKnownAnnotations.CreateTableAttribute))
            {
                return string.Empty;
            }

            var selectClause = string.Join(", ", type.Fields.Select(f => $@"{f.Name}{PostgresTypeConversion(f)}"));
            var insertClause = type.Fields.Select(FormatFieldValue);
            var ordinal = 0;
            
            var typeName = $"{type.Fqn()}";

            return $@"
public static {typeName} FromQuery(global::Npgsql.NpgsqlDataReader reader) =>
    new {typeName} (
{Indent(2, CreateReader(type, type.Fields)).TrimEnd()}
    );

public const string SelectClause = ""{selectClause}"";

{string.Join(Environment.NewLine, type.Fields.Select(f => $"public const int {f.PascalCase()}Ordinal = {ordinal++};"))}

public static string ComponentToDatabase(in {type.Fqn()} value)
{{
    return (
        $@""INSERT INTO {tableName} ({string.Join(",", type.Fields.Select(f => f.Name))}) VALUES ({string.Join(",", insertClause)});""
    );
}}

public struct DatabaseChangeNotification
{{
    public {type.Fqn()}? Old {{ get; set; }}

    public {type.Fqn()} New {{ get; set; }}
}}";
        }

        private static string CreateReader(TypeDescription type, IEnumerable<FieldDefinition> fields)
        {
            var sb = new StringBuilder();
            foreach (var field in fields)
            {
                var ordinal = $"{field.PascalCase()}Ordinal";
                var toAdd = field.TypeSelector switch
                {
#nullable disable
                    FieldType.Option => $"reader.IsDBNull({ordinal}) ? null : ({FqnFieldType(type, field)}) reader.{Types.SchemaToReaderMethod[field.OptionType.InnerType.Primitive]}({ordinal}),",
                    FieldType.Singular => $"reader.{Types.SchemaToReaderMethod[field.SingularType.Type.Primitive]}({ordinal}),",
#nullable restore
                    _ => throw new ArgumentOutOfRangeException()
                };

                sb.AppendLine(toAdd);
            }

            return sb.ToString().TrimEnd().TrimEnd(',');
        }

        private static string PostgresTypeConversion(FieldDefinition f) =>
#nullable disable
            f.TypeSelector switch
            {
                FieldType.Option when f.HasPrimitive(PrimitiveType.String) => "::text",
                FieldType.Option when f.HasPrimitive() => string.Empty,
                FieldType.Option => throw new InvalidOperationException($"Unsupported schema type '{f.OptionType.InnerType.Type}'. Only primitive types can be converted from Postgres."),

                FieldType.Singular when f.HasPrimitive(PrimitiveType.String) => "::text",
                FieldType.Singular when f.HasPrimitive() => string.Empty,
                FieldType.Singular => throw new InvalidOperationException($"Unsupported schema type '{f.SingularType.Type.Type}'. Only primitive types can be converted from Postgres."),
                _ => string.Empty
            };
#nullable restore

        private static string CreateColumns(string outerType, IEnumerable<FieldDefinition> fields)
        {
            var columnCreator = new StringBuilder();

            foreach (var field in fields)
            {
                var databaseType = field.Annotations.GetAnnotationString(WellKnownAnnotations.FieldTypeAttribute, 0);
                if (string.IsNullOrEmpty(databaseType))
                {
                    databaseType = field.TypeSelector switch
                    {
#nullable disable
                        FieldType.Option when field.HasEnum() => "integer",
                        FieldType.Option when field.HasPrimitive() => Types.SchemaToPostgresTypes[field.OptionType.InnerType.Primitive],
                        FieldType.Option when field.HasCustomType() => throw new Exception($"Unsupported schema type '{field.OptionType.InnerType.Type}'. Compound types are not supported."),

                        FieldType.List when field.HasEnum() => "integer[] not null",
                        FieldType.List when field.HasPrimitive() => $"{Types.SchemaToPostgresTypes[field.ListType.InnerType.Primitive]}[] not null",
                        FieldType.List when field.HasCustomType() => throw new Exception($"Unsupported schema type '{field.ListType.InnerType.Type}'. Compound types are not supported."),

                        FieldType.Map => throw new InvalidOperationException($"{outerType}.{field.Name}: Maps are not supported."),

                        FieldType.Singular when field.HasEnum() => "integer not null",
                        FieldType.Singular when field.HasPrimitive() => $"{Types.SchemaToPostgresTypes[field.SingularType.Type.Primitive]} not null",
                        FieldType.Singular when field.HasCustomType() => throw new Exception($"Unsupported schema type '{field.SingularType.Type.Type}'. Compound types are not supported."),

                        _ => throw new ArgumentOutOfRangeException()
#nullable restore
                    };
                }

                databaseType = databaseType.Replace("{fieldName}", field.PascalCase());

                if (string.IsNullOrEmpty(databaseType))
                {
                    columnCreator.AppendLine($"/* {field.Name} Skipped: \"\"{field.FieldId}\"\" integer */");
                }
                else
                {
                    columnCreator.AppendLine($"{field.Name} {databaseType},");
                }
            }

            return columnCreator.ToString().TrimEnd();
        }

        public string GenerateSql(TypeDescription type)
        {
            if (!HasAnnotation(type, WellKnownAnnotations.CreateTableAttribute))
            {
                return string.Empty;
            }

            var setupCommands = type.Annotations.GetAnnotationStrings(WellKnownAnnotations.CreateTableAttribute, 0);

            var primaryKeyFields = type.Fields.WithAnnotation(WellKnownAnnotations.PrimaryKeyAttribute).ToList();
            if (primaryKeyFields.Count == 0)
            {
                throw new Exception($"{type.QualifiedName} is exposed to the database, but no fields are marked with [{WellKnownAnnotations.PrimaryKeyAttribute}]");
            }

            var columnCreator = CreateColumns(type.QualifiedName, type.Fields);

            var primaryKeyColumnNames = string.Join(", ", primaryKeyFields.Select(f => $@"{f.Name}"));
            var primaryKey = $"PRIMARY KEY ({primaryKeyColumnNames})";

            var indexFields = type.Fields.WithAnnotation(WellKnownAnnotations.IndexAttribute);

            return $@"{string.Join(Environment.NewLine, setupCommands)}
CREATE TABLE {tableName} (
{columnCreator.TrimEnd()}
{primaryKey}
);

{string.Join(Environment.NewLine, indexFields.Select(f => f.Annotations.GetAnnotationString(WellKnownAnnotations.IndexAttribute, 0).Replace("{fieldName}", f.Name)))}

-- Setup change notifications. This maps to the DatabaseChangeNotification class.
CREATE OR REPLACE FUNCTION notify_{tableName}() RETURNS TRIGGER AS $$
    BEGIN
        IF TG_OP = 'UPDATE' THEN
            PERFORM pg_notify( '{tableName}'::text, json_build_object( 'old', row_to_json(OLD), 'new', row_to_json(NEW) )::text);
        ELSE
            PERFORM pg_notify( '{tableName}'::text, json_build_object( 'new', row_to_json(NEW) )::text);
        END IF;
        RETURN NEW;
    END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER notify_{tableName}_tgr
    AFTER INSERT OR UPDATE on {tableName}
    FOR EACH ROW EXECUTE PROCEDURE notify_{tableName}();
".Replace("{tableName}", tableName);
        }
    }
}
