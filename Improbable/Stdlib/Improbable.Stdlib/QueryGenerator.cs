using System;
using System.Collections.Generic;
using System.Linq;
using Pidgin;
using static Pidgin.Parser;

namespace Improbable.Stdlib
{
    public class QueryGenerator
    {
        private static readonly Parser<char, string> Star = Tok("*");
        private static readonly Parser<char, string> Comma = Tok(",");
        private static readonly Parser<char, string> EqualsToken = Tok("=");

        private static readonly Parser<char, char> UnderScore = Char('_');
        private static readonly Parser<char, char> Dot = Char('.');
        private static readonly Parser<char, char> Slash = Char('/');

        public static readonly Parser<char, string> ComponentName = Tok(Letter.Or(UnderScore))
            .Then(OneOf(LetterOrDigit, UnderScore, Dot).Many(), CharToIdentifier);

        public static readonly Parser<char, string> FieldName = Tok(Letter.Or(UnderScore))
            .Then(OneOf(LetterOrDigit, UnderScore).Many(), CharToIdentifier);

        public static readonly Parser<char, string> FullyQualifiedFieldName = ComponentName.Then(Slash, StringPlusChars).Then(FieldName, (s, s1) => s + s1);

        private static readonly Parser<char, string> ResultAll = Star.Labelled("<select all>");
        private static readonly Parser<char, IEnumerable<string>> ResultComponentList =
            Parenthesized(ComponentName.Separated(Comma))
                .Labelled("<select component list>");

        public static readonly Parser<char, int[]> VectorParser = Parenthesized(
                Map((x, c1, y, c3, z) => new[] {x, y, z},
                    OptionalValueEquals("x"),
                    Comma,
                    OptionalValueEquals("y"),
                    Comma,
                    OptionalValueEquals("z")))
            .Labelled("<vector3>");

        public static readonly Parser<char, int[]> SphereParser = Tok("sphere").Then(Parenthesized(
                ValueEquals("radius").Select(i => new[] {i})
                    .Or(Map((x, c1, y, c2, z, c3, r) => new[] {x, y, z, r},
                        ValueEquals("x"),
                        Comma,
                        ValueEquals("y"),
                        Comma,
                        ValueEquals("z"),
                        Comma,
                        ValueEquals("radius")))))
            .Labelled("<sphere>");

        public static readonly Parser<char, int[]> CylinderParser = Tok("cylinder").Then(Parenthesized(
                ValueEquals("radius").Select(i => new[] {i})
                    .Or(Map((x, c1, y, c2, z, c3, r) => new[] {x, y, z, r},
                        ValueEquals("x"),
                        Comma,
                        ValueEquals("y"),
                        Comma,
                        ValueEquals("z"),
                        Comma,
                        ValueEquals("radius")))))
            .Labelled("<cylinder>");

        public static readonly Parser<char, int[]> BoxParser = Tok("box").Then(Parenthesized(
                Vector3Equals("center")
                    .Then(Comma, PassThrough)
                    .Then(Vector3Equals("size"), CombineArrays)))
            .Labelled("<box>");

        private static Parser<char, T> Tok<T>(Parser<char, T> token)
        {
            return SkipWhitespaces.Then(Try(token).Before(SkipWhitespaces));
        }

        private static Parser<char, string> Tok(string token)
        {
            return Tok(CIString(token));
        }

        private static Parser<char, T> Parenthesized<T>(Parser<char, T> parser)
        {
            return parser.Between(Tok("("), Tok(")"));
        }

        private static Parser<char, T> Parenthesized<T>(IQueryReceiver q, Parser<char, T> parser)
        {
            return parser.Between(
                Tok("(").Select(s => q.PushConstraint()),
                Tok(")").Select(s => q.PopConstraint()))
                .Labelled("<scope>");
        }

        private static Parser<char, int> OptionalValueEquals(string valueName)
        {
            return Tok(valueName).Then(Tok(EqualsToken)).Optional().Then(DecimalNum.Before(SkipWhitespaces)).Labelled($"({valueName}=)");
        }

        private static Parser<char, int> ValueEquals(string valueName)
        {
            return Tok(valueName).Then(Tok(EqualsToken)).Optional().Then(DecimalNum.Before(SkipWhitespaces)).Labelled($"{valueName}=");
        }

        private static Parser<char, int[]> Vector3Equals(string valueName)
        {
            return Tok(valueName).Labelled($"{valueName}=").Then(EqualsToken).Then(VectorParser);
        }

        public static Parser<char, IQueryReceiver> SelectStatement(IQueryReceiver q)
        {
            return Tok("select").Then(OneOf(
                    ResultAll.Select(s => q.OnAllResult()),
                    ResultComponentList.Select(q.OnComponentsResults)))
                .Labelled("<select>");
        }

        public static Parser<char, IQueryReceiver> AndConstraint(IQueryReceiver q)
        {
            return Tok("and").Select(s => q.OnAndConstraint()).Then(Rec(() => Constraint(q))).Labelled("<and>");
        }

        public static Parser<char, IQueryReceiver> OrConstraint(IQueryReceiver q)
        {
            return Tok("or").Select(s => q.OnOrConstraint()).Then(Rec(() => Constraint(q))).Labelled("<or>");
        }

        public static Parser<char, IQueryReceiver> NotConstraint(IQueryReceiver q)
        {
            return Tok("not").Select(s => q.OnNotConstraint()).Then(Rec(() => Constraint(q))).Labelled("<not>");
        }

        public static Parser<char, IQueryReceiver> PositionInConstraint(IQueryReceiver q)
        {
            return Tok("in").Then(
                OneOf(
                    SphereParser.Select(q.OnSphere),
                    CylinderParser.Select(q.OnCylinder),
                    BoxParser.Select(q.OnBox)))
                .Labelled("<in>");
        }

        public static readonly Parser<char, long> EntityIdConstraint = Tok("entityid").Then(EqualsToken).Then(LongNum);
        public static readonly Parser<char, string> ComponentConstraint = Tok("has_component").Then(ComponentName);

        public static Parser<char, IQueryReceiver> SpecificConstraints(IQueryReceiver q)
        {
            
            return OneOf(
                PositionInConstraint(q),
                EntityIdConstraint.Select(q.OnEntityId),
                ComponentConstraint.Select(q.OnComponentConstraint))
                .Labelled("<specific_constraint>");
        }

        public static Parser<char, IQueryReceiver> CompoundConstraints(IQueryReceiver q)
        {
            return AndConstraint(q).AtLeastOnce()
                .Or(OrConstraint(q).AtLeastOnce())
                .Optional()
                .Select(receivers => q)
                .Labelled("<compound_constraint>");
        }

        public static Parser<char, IQueryReceiver> Constraint(IQueryReceiver q)
        {
            return OneOf(
                Parenthesized(q, Rec(() => Constraint(q))),
                    SpecificConstraints(q)
                )
                .Then(CompoundConstraints(q))
                .Labelled("<constraint>");
        }

        private static string StringPlusChars(string s, char c)
        {
            return s + string.Concat(c);
        }

        private static string CharToIdentifier(char c1, IEnumerable<char> chars)
        {
            return string.Concat(c1) + string.Concat(chars);
        }

        private static T[] CombineArrays<T>(T[] first, T[] second)
        {
            return first.Concat(second).ToArray();
        }

        private static int[] PassThrough(int[] values, string s)
        {
            return values;
        }

        public static T Parse<T>(string query, T q) where T : IQueryReceiver
        {
            var result = SelectStatement(q)
                .Then(Tok("where"))
                .Then(Constraint(q))
                .Parse(query);

            if (!result.Success)
            {
                var unexpected = "";
                if (result.Error.Unexpected.HasValue)
                {
                    unexpected = $"Unexpected '{result.Error.Unexpected.Value}'\n";
                }

                throw new Exception($@"Parse error: {result.Error.Message}
{unexpected}Expected {string.Join("", result.Error.Expected)}
{query}
{string.Join("", Enumerable.Repeat("-", result.Error.ErrorPos.Col - 1))}^
");
            }

            return q;
        }

        public interface IQueryReceiver
        {
            IQueryReceiver OnAllResult();
            IQueryReceiver OnComponentsResults(IEnumerable<string> componentNames);
            IQueryReceiver OnAndConstraint();
            IQueryReceiver OnOrConstraint();
            IQueryReceiver OnNotConstraint();
            IQueryReceiver PushConstraint();
            IQueryReceiver PopConstraint();
            IQueryReceiver OnSphere(IEnumerable<int> param);
            IQueryReceiver OnCylinder(IEnumerable<int> param);
            IQueryReceiver OnBox(IEnumerable<int> param);
            IQueryReceiver OnEntityId(long param);
            IQueryReceiver OnComponentConstraint(string componentName);

        }
    }
}
