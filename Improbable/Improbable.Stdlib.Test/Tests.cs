using System.Collections.Generic;
using NUnit.Framework;
using Pidgin;

namespace Improbable.Stdlib.Test
{
    public class Tests
    {
        [Test]
        [TestCase("a")]
        [TestCase("_")]
        [TestCase("improbable")]
        [TestCase("_improbable")]
        [TestCase("_impro_bable")]
        [TestCase("_impro_1bable2")]
        [TestCase("improbable.ComponentName")]
        [TestCase("improbable.ComponentName1")]
        [TestCase("improbable.ComponentName_1")]
        public void Test_CanParseComponentNames(string testCase)
        {
            Assert.That(QueryGenerator.ComponentName.ParseOrThrow(testCase), Is.EqualTo(testCase));
        }

        [Test]
        [TestCase("1")]
        [TestCase("[")]
        [TestCase(".")]
        [TestCase("1improbable")]
        [TestCase("[improbable")]
        [TestCase(".improbable")]
        public void Test_RejectInvalidComponentNames(string testCase)
        {
            Assert.Throws<ParseException>(() => QueryGenerator.ComponentName.ParseOrThrow(testCase));
        }

        [Test]
        [TestCase("improbable")]
        [TestCase("_improbable")]
        [TestCase("_impro_bable")]
        [TestCase("_impro_1bable2")]
        public void Test_CanParseFieldName(string testCase)
        {
            Assert.That(QueryGenerator.FieldName.ParseOrThrow(testCase), Is.EqualTo(testCase));
        }

        [Test]
        [TestCase("1improbable")]
        [TestCase(".improbable")]
        [TestCase("]impro_bable")]
        public void Test_RejectInvalidFieldName(string testCase)
        {
            Assert.Throws<ParseException>(() => QueryGenerator.FieldName.ParseOrThrow(testCase));
        }

        [Test]
        [TestCase("improbable.Component/fieldname")]
        public void Test_CanParseFullyQualifiedFieldName(string testCase)
        {
            Assert.That(QueryGenerator.FullyQualifiedFieldName.ParseOrThrow(testCase), Is.EqualTo(testCase));
        }

        [Test]
        [TestCase("select *", new [] { "$all"})]
        [TestCase("select count(*)", new [] { "$count"})]
        [TestCase("select improbable.Position", new [] { "improbable.Position"})]
        [TestCase("select improbable.Position,improbable.Metadata", new [] { "improbable.Position", "improbable.Metadata" })]
        public void Test_CanParseResultTypes(string query, IEnumerable<string> resultTokens)
        {
            Assert.That(QueryGenerator.SelectStatement.ParseOrThrow(query), Is.EquivalentTo(resultTokens));
        }

        [Test]
        [TestCase("sphere(x=10,y=20,z=30,radius=40)", new[] { 10, 20, 30, 40 })]
        [TestCase("sphere(radius=40)", new[] { 40 })]
        [TestCase("sphere ( x = 10 , y = 20 , z = 30 , radius = 40 )", new[] { 10, 20, 30, 40 })]
        public void Test_CanParseSphereConstraints(string query, IEnumerable<int> resultTokens)
        {
            Assert.That(QueryGenerator.SphereParser.ParseOrThrow(query), Is.EquivalentTo(resultTokens));
        }

        [Test]
        [TestCase("cylinder(radius=40)", new[] { 40 })]
        [TestCase("cylinder(x=10,y=20,z=30,radius=40)", new[] { 10, 20, 30, 40 })]
        [TestCase("cylinder ( x = 10 , y = 20, z = 30 , radius = 40 )", new[] { 10, 20, 30, 40 })]
        public void Test_CanParseCylinderConstraints(string query, IEnumerable<int> resultTokens)
        {
            Assert.That(QueryGenerator.CylinderParser.ParseOrThrow(query), Is.EquivalentTo(resultTokens));
        }

        [Test]
        [TestCase("box((1,2,3),(4,5,6))", new[] { 1,2,3,4,5,6})]
        [TestCase("box(center=(1,2,3),size=(4,5,6))", new[] { 1,2,3,4,5,6})]
        [TestCase("box(center=(x=1,y=2,z=3),size=(x=4,y=5,z=6))", new[] { 1,2,3,4,5,6})]
        [TestCase("box ( center = ( x = 1 , y = 2 , z = 3 ) , size = ( x = 4 , y = 5 , z = 6 ) )", new[] { 1, 2, 3, 4, 5, 6 })]
        public void Test_CanParseBoxConstraints(string query, IEnumerable<int> resultTokens)
        {
            Assert.That(QueryGenerator.BoxParser.ParseOrThrow(query), Is.EquivalentTo(resultTokens));
        }
    }
}
