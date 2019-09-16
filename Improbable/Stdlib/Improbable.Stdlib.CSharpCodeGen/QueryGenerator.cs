using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Improbable.CSharpCodeGen.Case;

namespace Improbable.Stdlib.CSharpCodeGen
{
    class Scope
    {
        public string Constraint = "";
        public List<string> Items = new List<string>();
    }

    public class QueryGenerator : Stdlib.QueryGenerator.IQueryReceiver
    {
        private readonly StringBuilder constraint = new StringBuilder();
        private readonly StringBuilder fullText = new StringBuilder();
        private readonly Stack<Scope> scopes = new Stack<Scope>();
        private string resultType;

        public QueryGenerator(string query, string funcName)
        {
            Query = query;
            PushConstraint();

            Stdlib.QueryGenerator.Parse(query, this);

            PopConstraint();

            fullText.Append($@"public static global::Improbable.ComponentInterest.Query {funcName}()
{{
    return new global::Improbable.ComponentInterest.Query(constraint:
{Indent(2, constraint.ToString().Trim())},
        {resultType}
    );
}}
");
        }

        public string Text => fullText.ToString();
        public string Query { get; }

        public Stdlib.QueryGenerator.IQueryReceiver OnAllResult()
        {
            resultType = "fullSnapshotResult: true";
            return this;
        }

        public Stdlib.QueryGenerator.IQueryReceiver OnComponentsResults(IEnumerable<string> componentNames)
        {
            resultType = $"resultComponentId: new [] {{{string.Join(",", componentNames)}}}";
            return this;
        }

        public Stdlib.QueryGenerator.IQueryReceiver OnAndConstraint()
        {
            scopes.Peek().Constraint = "andConstraint:";
            return this;
        }

        public Stdlib.QueryGenerator.IQueryReceiver OnOrConstraint()
        {
            scopes.Peek().Constraint = "orConstraint:";
            return this;
        }

        public Stdlib.QueryGenerator.IQueryReceiver OnNotConstraint()
        {
            return this;
        }

        public Stdlib.QueryGenerator.IQueryReceiver PushConstraint()
        {
            scopes.Push(new Scope());
            return this;
        }

        public Stdlib.QueryGenerator.IQueryReceiver PopConstraint()
        {
            var scope = scopes.Pop();

            var sb = new StringBuilder();

            if (string.IsNullOrEmpty(scope.Constraint))
            {
                sb.AppendLine(Indent(scopes.Count + 1, $"{ToConstraint(scope.Items[0])}"));
            }
            else
            {
                var items = scope.Items.Select(ToConstraint);
                sb.Append($@"{scope.Constraint} new []
 {Indent(1, "{")}
{Indent(2, string.Join(",\n", items))}
{Indent(1, "}")}");
            }

            if (scopes.Count == 0)
            {
                // We've hit the root of the expression.
                constraint.Append(ToConstraint(sb.ToString()));
            }
            else
            {
                scopes.Peek().Items.Add(sb.ToString());
            }
            
            return this;
        }

        private string ToConstraint(string i)
        {
            return $"new global::Improbable.ComponentInterest.QueryConstraint({i})";
        }

        public Stdlib.QueryGenerator.IQueryReceiver OnSphere(IEnumerable<int> param)
        {
            var args = param.ToArray();
            if (args.Length == 1)
            {
                scopes.Peek().Items.Add($"relativeSphereConstraint: new global::Improbable.ComponentInterest.RelativeSphereConstraint({args[0]})");
            }
            else
            {
                scopes.Peek().Items.Add($"sphereConstraint: new global::Improbable.ComponentInterest.SphereConstraint(new global::Improbable.Coordinates({string.Join(", ", args.Take(3))}), {args[3]})");
            }

            return this;
        }

        public Stdlib.QueryGenerator.IQueryReceiver OnCylinder(IEnumerable<int> param)
        {
            var args = param.ToArray();
            if (args.Length == 1)
            {
                scopes.Peek().Items.Add($"relativeCylinderConstraint: new global::Improbable.ComponentInterest.RelativeCylinderConstraint({args[0]})");
            }
            else
            {
                scopes.Peek().Items.Add($"cylinderConstraint: new global::Improbable.ComponentInterest.CylinderConstraint(new global::Improbable.Coordinates({string.Join(", ", args.Take(3))}), {args[3]})");
            }

            return this;
        }

        public Stdlib.QueryGenerator.IQueryReceiver OnBox(IEnumerable<int> param)
        {
            var args = param.ToArray();
            scopes.Peek().Items.Add($"boxConstraints: new global::Improbable.ComponentInterest.BoxConstraint(new global::Improbable.Coordinates({string.Join(", ", args.Take(3))}), new global::Improbable.EdgeLength({string.Join(", ", args.Skip(3))}))");

            return this;
        }

        public Stdlib.QueryGenerator.IQueryReceiver OnEntityId(long param)
        {
            scopes.Peek().Items.Add($"entityIdConstraint: {param}");
            return this;
        }

        public Stdlib.QueryGenerator.IQueryReceiver OnComponentConstraint(string componentName)
        {
            scopes.Peek().Items.Add($"componentConstraint: \"{componentName}\"");
            return this;
        }
    }
}
