using System;
using System.Collections.Generic;
using System.Linq;

namespace Improbable.CSharpCodeGen
{
    public static class Case
    {
        public static string CapitalizeFirstLetter(string text)
        {
            return char.ToUpperInvariant(text[0]) + text.Substring(1, text.Length - 1);
        }

        public static string ToPascalCase(IEnumerable<string> parts)
        {
            var result = string.Empty;
            foreach (var s in parts)
            {
                result += CapitalizeFirstLetter(s);
            }

            return result;
        }

        public static string SnakeCaseToPascalCase(string text)
        {
            return ToPascalCase(text.Split("_", StringSplitOptions.RemoveEmptyEntries));
        }

        public static string SnakeCaseToCamelCase(string text)
        {
            var parts = text.Split("_", StringSplitOptions.RemoveEmptyEntries);
            return parts[0] + ToPascalCase(parts.Skip(1));
        }

        public static string Fqn(string name)
        {
            var strings = name.Split(".", StringSplitOptions.RemoveEmptyEntries);
            return $"global::{string.Join(".", strings.Select(SnakeCaseToPascalCase))}";
        }

        internal static string Namespace(string qualifiedName)
        {
            var strings = qualifiedName.Split(".", StringSplitOptions.RemoveEmptyEntries);
            return string.Join(".", strings
                .Take(strings.Length - 1)
                .Select(SnakeCaseToPascalCase));
        }

        public static string Indent(int level, string inputString)
        {
            var indent = string.Empty.PadLeft(level, '\t');
            return indent + inputString.Replace("\n", $"\n{indent}");
        }
    }
}
