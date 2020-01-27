using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Improbable.Schema.Bundle;
using static Improbable.CSharpCodeGen.Case;
using static Improbable.CSharpCodeGen.Types;

namespace Improbable.CSharpCodeGen
{
    public class Generator : ICodeGenerator
    {
        private readonly Bundle bundle;

        public Generator(Bundle bundle)
        {
            this.bundle = bundle;
            FieldDecorators = new List<Func<FieldDefinition, string>>();
        }

        public List<Func<FieldDefinition, string>> FieldDecorators { get; }

        public string Generate(TypeDescription type)
        {
            var sb = new StringBuilder();

            if (type.ComponentId.HasValue)
            {
                sb.AppendLine($"public const uint ComponentId = {type.ComponentId.Value};");
            }

            sb.AppendLine(GenerateFields(type, type.Fields));

            // For types with a single field of map or list type, provide a params-style constructor for nicer ergonomics.
            if (type.Fields.Length == 1 && (type.Fields[0].IsList() || type.Fields[0].IsMap()))
            {
                sb.AppendLine(GenerateParamsConstructor(type, type.Fields));
            }

            sb.AppendLine(GenerateEquatable(type, type.Fields));

            return sb.ToString();
        }

        private static string GenerateParamsConstructor(in TypeDescription type, IReadOnlyList<FieldDefinition> fields)
        {
            var field = fields[0];

            return $@"
public {type.TypeName()}(params {field.InnerFqn()}[] {field.CamelCase()})
{{
    {field.PascalCase()} = {InitializeFromParameter(type, field)};
}}";

        }

        public string GenerateEquatable(TypeDescription type, IReadOnlyList<FieldDefinition> fields)
        {
            var hashFields = new StringBuilder();
            var equalsFields = new StringBuilder();
            var typeName = type.TypeName();

            if (!fields.Any())
            {
                hashFields.AppendLine("hashCode = base.GetHashCode();");
                equalsFields.AppendLine("base.Equals(other)");
            }
            else
            {
                foreach (var field in fields)
                {
                    hashFields.AppendLine($"hashCode = (hashCode * 397) ^ ({FieldToHash(field)});");
                }

                for (var i = 0; i < fields.Count; i++)
                {
                    var field = fields[i];
                    equalsFields.Append($"{FieldToEquals(field)}");
                    if (i < fields.Count - 1)
                    {
                        equalsFields.AppendLine(" &&");
                    }
                }
            }

            return
                $@"public override int GetHashCode()
{{
    unchecked
    {{
        var hashCode = 0;
{Indent(2, hashFields.ToString().TrimEnd())}
        return hashCode;
    }}
}}

public bool Equals({typeName} other)
{{
    return
{Indent(2, equalsFields.ToString())};
}}

public override bool Equals(object obj)
{{
    if (ReferenceEquals(null, obj))
    {{
        return false;
    }}

    return obj is {typeName} other && Equals(other);
}}

public static bool operator ==({typeName} a, {typeName} b)
{{
    return a.Equals(b);
}}

public static bool operator !=({typeName} a, {typeName} b)
{{
    return !a.Equals(b);
}}
";
        }

        private string GenerateFields(TypeDescription type, IEnumerable<FieldDefinition> fields)
        {
            var fieldText = new StringBuilder();
            foreach (var field in fields)
            {
                foreach (var decorator in FieldDecorators)
                {
                    fieldText.AppendLine(decorator(field));
                }

                fieldText.AppendLine($"public readonly {FqnFieldType(type, field)} {field.PascalCase()};");
            }

            return fieldText.ToString();
        }
    }
}
