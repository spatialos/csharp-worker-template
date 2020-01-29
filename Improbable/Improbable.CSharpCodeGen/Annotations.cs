using System;
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

        public static IEnumerable<string> GetAnnotationStrings(this IEnumerable<Annotation> annotations, string attributeName, int fieldIndex)
        {
            var instance = annotations.Where(a => a.TypeValue.Type == attributeName).ToArray();
            if (!instance.Any())
            {
                return Array.Empty<string>();
            }

            var firstValue = instance.First();
            var valueList = firstValue.TypeValue.Fields[fieldIndex].Value.ListValue;
            if (valueList == null || valueList.Values.Any(v => v.StringValue == null))
            {
                throw new InvalidOperationException($"{firstValue.TypeValue.Type} is not a list<string> type.");
            }

            return valueList.Values.Select(v => v.StringValue ?? string.Empty);
        }

        public static string GetAnnotationString(this IEnumerable<Annotation> annotations, string attributeName, int fieldIndex)
        {
            var instance = annotations.Where(a => a.TypeValue.Type == attributeName).ToArray();
            if (!instance.Any())
            {
                return string.Empty;
            }

            var firstValue = instance.First();
            var value = firstValue.TypeValue.Fields[fieldIndex].Value.StringValue;
            return value ?? string.Empty;
        }

        public static IEnumerable<FieldDefinition> WithAnnotation(this IEnumerable<FieldDefinition> fields, string fieldIndex)
        {
            return fields.Where(f => HasAnnotations(f, fieldIndex));
        }
    }
}
