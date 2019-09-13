using System;
using System.Collections.Generic;
using System.Linq;
using Pidgin;
using static Pidgin.Parser;

namespace Improbable.Stdlib
{
    public class QueryGenerator
    {
        private static Parser<char, T> Tok<T>(Parser<char, T> token)
            => Try(token).Before(SkipWhitespaces);

        private static Parser<char, string> Tok(string token)
            => Tok(CIString(token));

        private static Parser<char, T> Parenthesized<T>(Parser<char, T> parser)
            => parser.Between(Tok("("), Tok(")"));

        private static readonly Parser<char, string> Select = Tok("select");
        private static readonly Parser<char, string> OpenParen = Tok("(");
        private static readonly Parser<char, string> CloseParen = Tok(")");
        private static readonly Parser<char, string> Star = Tok("*");
        private static readonly Parser<char, string> Count = Tok("count");
        private static readonly Parser<char, string> And = Tok("and");
        private static readonly Parser<char, string> Or = Tok("or");
        private static readonly Parser<char, string> Not = Tok("not");
        private static readonly Parser<char, string> Position = Tok("position");
        private static readonly Parser<char, string> Within = Tok("within");
        private static readonly Parser<char, string> Sphere = Tok("sphere");
        private static readonly Parser<char, string> Cylinder = Tok("cylinder");
        private static readonly Parser<char, string> Box = Tok("box");
        private static readonly Parser<char, string> Comma = Tok(",");
        private static readonly Parser<char, string> EqualsToken = Tok("=");

        private static readonly Parser<char, char> UnderScore = Char('_');
        private static readonly Parser<char, char> Dot = Char('.');
        private static readonly Parser<char, char> Slash = Char('/');

        public static readonly Parser<char, string> ComponentName = Tok(Letter.Or(UnderScore))
            .Then(OneOf(LetterOrDigit, UnderScore, Dot).Many(), CharToIdentifier);

        public static readonly Parser<char, string> FieldName = Tok(Letter.Or(UnderScore))
            .Then(OneOf(LetterOrDigit, UnderScore).Many(), CharToIdentifier);

        private static Parser<char, int> ValueEquals(string valueName) => Tok(valueName).Then(Tok(EqualsToken)).Optional().Then(DecimalNum.Before(SkipWhitespaces)).Labelled(valueName);
        private static Parser<char, int[]> Vector3Equals(string valueName) => Tok(valueName).Then(EqualsToken).Optional().Then(VectorParser).Labelled(valueName);

        public static readonly Parser<char, string> FullyQualifiedFieldName = ComponentName.Then(Slash, StringPlusChars).Then(FieldName, (s, s1) => s + s1);

        private static readonly Parser<char, string> ResultAll = Star;
        private static readonly Parser<char, string> ResultCount = Count.Then(OpenParen).Then(Star).Then(CloseParen);
        private static readonly Parser<char, IEnumerable<string>> ResultComponentList = ComponentName.Separated(Comma);

        public static Parser<char, IQueryReceiver> SelectStatement(IQueryReceiver q) => Select.Then(OneOf(
            ResultAll.Select(s => q.OnAllResult()),
            ResultCount.Select(s => q.OnCountResult()),
            ResultComponentList.Select(q.OnComponentsResults)));

        public static Parser<char, IQueryReceiver> AndConstraint(IQueryReceiver q) => Tok(And.Then(Rec(() => Constraint(q))));
        public static Parser<char, IQueryReceiver> OrConstraint(IQueryReceiver q) => Tok(Or.Then(Rec(() => Constraint(q))));
        public static Parser<char, IQueryReceiver> NotConstraint(IQueryReceiver q) => Tok(Not.Then(Rec(() => Constraint(q))));

        public static readonly Parser<char, int[]> SphereParser = Sphere.Then(Parenthesized(
            ValueEquals("radius").Select(i => new[] {i})
                .Or(Map((x, c1, y, c2, z, c3, r) => new[] {x, y, z, r},
                    ValueEquals("x"),
                    Comma,
                    ValueEquals("y"),
                    Comma,
                    ValueEquals("z"),
                    Comma,
                    ValueEquals("radius")))));

        public static readonly Parser<char, int[]> CylinderParser = Cylinder.Then(Parenthesized(
            ValueEquals("radius").Select(i => new[] {i})
                .Or(Map((x, c1, y, c2, z, c3, r) => new[] {x, y, z, r},
                    ValueEquals("x"),
                    Comma,
                    ValueEquals("y"),
                    Comma,
                    ValueEquals("z"),
                    Comma,
                    ValueEquals("radius")))));

        public static readonly Parser<char, int[]> VectorParser = Parenthesized(
                Map((x, c1, y, c3, z) => new[] { x, y, z },
                    ValueEquals("x"),
                    Comma,
                    ValueEquals("y"),
                    Comma,
                    ValueEquals("z")));

        public static readonly Parser<char, int[]> BoxParser = Box.Then(Parenthesized(
            Vector3Equals("center")
                .Or(
                    Vector3Equals("center")
                    .Then(Comma, PassThrough)
                    .Then(Vector3Equals("size"), CombineArrays))));

        private static T[] CombineArrays<T>(T[] first, T[] second)
        {
            return first.Union(second).ToArray();
        }

        private static int[] PassThrough(int[] values, string s)
        {
            return values;
        }

        public static readonly Parser<char, int[]> PositionWithinConstraint = Position.Then(Within).Then(
            OneOf(SphereParser, CylinderParser, BoxParser));

        public static Parser<char, IQueryReceiver> Constraint(IQueryReceiver q) => Tok(OneOf(
            Rec(() => Constraint(q)),
            Parenthesized(Rec(() => Constraint(q)))).Then(OneOf(AndConstraint(q), OrConstraint(q), NotConstraint(q))).Many().Select(qs => q);

        private static string StringPlusChars(string s, char c)
        {
            return s + string.Concat(c);
        }

        private static string CharToIdentifier(char c1, IEnumerable<char> chars)
        {
            return string.Concat(c1) + string.Concat(chars);
        }

        public static void Parse(string query, IQueryReceiver q)
        {
            SelectStatement(q)
                .Then(Tok("where"))
                .Then(Constraint(q))
        }

        public interface IQueryReceiver
        {
            IQueryReceiver OnAllResult();
            IQueryReceiver OnCountResult();
            IQueryReceiver OnComponentsResults(IEnumerable<string> componentNames);
            IQueryReceiver OnAndConstraint()
        }
    }
}
