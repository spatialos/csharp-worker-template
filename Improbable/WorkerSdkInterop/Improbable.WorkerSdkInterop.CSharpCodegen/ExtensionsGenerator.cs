using Improbable.CSharpCodeGen;
using Improbable.Schema.Bundle;

namespace Improbable.WorkerSdkInterop.CSharpCodeGen
{
    public class ExtensionsGenerator
    {
        public static string Generate(TypeDescription type)
        {
            return !type.ComponentId.HasValue ? string.Empty : $@"namespace {type.Namespace()}
{{
    public static partial class Extensions
    {{
        public static void Add(this global::Improbable.Worker.CInterop.Entity entity, in {type.TypeName()} component)
        {{
            entity.Add(component.ToData());
        }}
    }}
}}";

            // Convenience
        }
    }
}
