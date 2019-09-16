using System.IO;
using Improbable.CSharpCodeGen;
using Improbable.Stdlib.CSharpCodeGen;

namespace QueryRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            var gen = new QueryGenerator("select * where (entityid=999 and in sphere(x=0,y=0,z=0,radius=100)) or in cylinder(x=1,y=1,z=1,radius=2)", "MyQuery");
            //var gen = new QueryGenerator("select * where entityid=999", "MyQuery");
            File.WriteAllText(@"c:\git\database_sync_worker_example\GeneratedCode\gen\Query.cs", $@"namespace Improbable
{{
    public static class Queries
    {{
{Case.Indent(2, $"// {gen.Query}")}
{Case.Indent(2, gen.Text)}        
    }}
}}");
        }
    }
}
