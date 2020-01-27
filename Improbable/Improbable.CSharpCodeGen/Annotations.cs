using System.Collections.Generic;
using System.Linq;
using Improbable.Schema.Bundle;

namespace Improbable.CSharpCodeGen
{
    public static class Annotations
    {
        public static bool HasAnnotations(FieldDefinition f, params string[] attributeNames)
        {
            return f.Annotations.Select(a => a.TypeValue.Type).Intersect(attributeNames).Any();
        }

        public static IEnumerable<string> GetAnnotationStrings(this IEnumerable<Annotation> annotations, string attributeName, int fieldNumber)
        {
            var annotation = annotations.Where(a => a.TypeValue.Type == attributeName).ToArray();
            if (!annotation.Any())
            {
                return new string[]{};
            }

            var list = annotation.First().TypeValue.Fields[fieldNumber].Value.ListValue.Values;
            return list.Select(v => v.StringValue);
        }

        public static string GetAnnotationString(this IEnumerable<Annotation> annotations, string attributeName, int fieldIndex)
        {
            var annotation = annotations.Where(a => a.TypeValue.Type == attributeName).ToArray();
            return !annotation.Any() ?
                string.Empty :
                annotation.First().TypeValue.Fields[fieldIndex].Value.StringValue;
        }

        public static IEnumerable<FieldDefinition> WithAnnotation(this IEnumerable<FieldDefinition> fields, string fieldIndex)
        {
            return fields.Where(f => HasAnnotations(f, fieldIndex));
        }
    }
}
