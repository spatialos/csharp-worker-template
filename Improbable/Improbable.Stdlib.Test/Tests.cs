using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pidgin;

namespace Improbable.Stdlib.Test
{
    [Parallelizable(ParallelScope.All)]
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
        public void Test_CanParseComponentNamesWithTrailingTokens()
        {
            Assert.That(QueryGenerator.ComponentName.ParseOrThrow("improbable.ComponentName_1 and"), Is.EqualTo("improbable.ComponentName_1"));
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
        [TestCase("entityid=999", 999)]
        [TestCase("entityid = 999", 999)]
        public void Test_EntityIdConstraint(string testCase, long entityId)
        {
            Assert.That(QueryGenerator.EntityIdConstraint.ParseOrThrow(testCase), Is.EqualTo(entityId));

        }

        [Test]
        [TestCase("has_component improbable.Atlanta", "improbable.Atlanta")]
        public void Test_ComponentConstraint(string testCase, string componentName)
        {
            Assert.That(QueryGenerator.ComponentConstraint.ParseOrThrow(testCase), Is.EqualTo(componentName));
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
        [TestCase("sphere(x=10,y=20,z=30,radius=40)", new[] { 10, 20, 30, 40 })]
        [TestCase("sphere(radius=40)", new[] { 40 })]
        [TestCase("sphere ( x = 10 , y = 20 , z = 30 , radius = 40 )", new[] { 10, 20, 30, 40 })]
        public void Test_CanParseSphereConstraints(string query, IEnumerable<int> resultTokens)
        {
            Assert.That(QueryGenerator.SphereParser.ParseOrThrow(query), Is.EqualTo(resultTokens));
        }

        [Test]
        [TestCase("cylinder(radius=40)", new[] { 40 })]
        [TestCase("cylinder(x=10,y=20,z=30,radius=40)", new[] { 10, 20, 30, 40 })]
        [TestCase("cylinder ( x = 10 , y = 20, z = 30 , radius = 40 )", new[] { 10, 20, 30, 40 })]
        public void Test_CanParseCylinderConstraints(string query, IEnumerable<int> resultTokens)
        {
            Assert.That(QueryGenerator.CylinderParser.ParseOrThrow(query), Is.EqualTo(resultTokens));
        }

        [Test]
        [TestCase("box(center=(1,2,3),size=(4,5,6))", new[] { 1,2,3,4,5,6})]
        [TestCase("box(center=(x=1,y=2,z=3),size=(x=4,y=5,z=6))", new[] { 1,2,3,4,5,6})]
        [TestCase("box ( center = ( x = 1 , y = 2 , z = 3 ) , size = ( x = 4 , y = 5 , z = 6 ) )", new[] { 1, 2, 3, 4, 5, 6 })]
        public void Test_CanParseBoxConstraints(string query, IEnumerable<int> resultTokens)
        {
            Assert.That(QueryGenerator.BoxParser.ParseOrThrow(query), Is.EqualTo(resultTokens));
        }

        [Test]
        [TestCase("select * where in sphere(x=0,y=0,z=0,radius=100)",
            new [] {"$all", "$sphere(0,0,0,100)"})]

        [TestCase("select (improbable.Position,improbable.Metadata) where in sphere(x=0,y=0,z=0,radius=100)",
            new[] { "improbable.Position", "improbable.Metadata", "$sphere(0,0,0,100)" })]

        [TestCase("select * where (in sphere(x=0,y=0,z=0,radius=100))",
            new[] { "$all", "$push", "$sphere(0,0,0,100)", "$pop" })]

        [TestCase("select * where in box(center=(0,0,0),size=(1,1,1))",
            new[] { "$all", "$box(0,0,0,1,1,1)" })]

        [TestCase("select * where entityid=999 and entityid=88",
            new[] { "$all", "$entityid(999)", "$and", "$entityid(88)" })]

        [TestCase("select * where has_component improbable.EntityAcl and entityid=999",
            new[] { "$all", "$has_component(improbable.EntityAcl)", "$and", "$entityid(999)" })]

        [TestCase("select * where (entityid=999 and in sphere(x=0,y=0,z=0,radius=100)) or in cylinder(x=1,y=1,z=1,radius=2)",
            new[] { "$all", "$push", "$entityid(999)", "$and", "$sphere(0,0,0,100)", "$pop", "$or", "$cylinder(1,1,1,2)" })]

        [TestCase("select * where in sphere(x=0,y=0,z=0,radius=100) and in sphere(x=0,y=0,z=0,radius=200)",
            new [] {"$all", "$sphere(0,0,0,100)", "$and", "$sphere(0,0,0,200)" })]

        [TestCase("select * where in sphere(x=0,y=0,z=0,radius=100) or in sphere(x=0,y=0,z=0,radius=200)",
            new[] { "$all", "$sphere(0,0,0,100)", "$or", "$sphere(0,0,0,200)" })]

        [TestCase("select * where has_component improbable.EntityAcl and in sphere(x=0,y=0,z=0,radius=100) and in sphere(x=0,y=0,z=0,radius=200)",
            new [] {"$all", "$has_component(improbable.EntityAcl)", "$and", "$sphere(0,0,0,100)", "$and", "$sphere(0,0,0,200)" })]

        [TestCase("select * where (in sphere(x=0,y=0,z=0,radius=100) and in sphere(x=0,y=0,z=0,radius=200))",
            new [] {"$all", "$push", "$sphere(0,0,0,100)", "$and", "$sphere(0,0,0,200)","$pop" })]
        public void Test_QueryReceiver(string query, IEnumerable<string> ops)
        {
            var recorder = new QueryRecorder();
            QueryGenerator.Parse(query, recorder);

            Assert.That(recorder.Ops, Is.EqualTo(ops));
        }

        [Test]
        [TestCase("select *", new [] { "$all"})]
        [TestCase("select (improbable.Component)", new [] { "improbable.Component"})]
        [TestCase("select (improbable.Component1, improbable.Component2)", new [] { "improbable.Component1", "improbable.Component2"})]
        public void Test_ComponentList(string query, IEnumerable<string> components)
        {
            var recorder = new QueryRecorder();
            QueryGenerator.SelectStatement(recorder).ParseOrThrow(query);
            Assert.That(recorder.Ops, Is.EqualTo(components));
        }

        private class QueryRecorder : QueryGenerator.IQueryReceiver
        {
            public List<string> Ops { get; }

            public QueryRecorder()
            {
                Ops = new List<string>();
            }

            public QueryGenerator.IQueryReceiver OnAllResult()
            {
                Ops.Add("$all");
                return this;
            }

            public QueryGenerator.IQueryReceiver OnCountResult()
            {
                Ops.Add("$count");
                return this;
            }

            public QueryGenerator.IQueryReceiver OnComponentsResults(IEnumerable<string> componentNames)
            {
                Ops.AddRange(componentNames);
                return this;
            }

            public QueryGenerator.IQueryReceiver OnAndConstraint()
            {
                Ops.Add("$and");
                return this;
            }

            public QueryGenerator.IQueryReceiver OnOrConstraint()
            {
                Ops.Add("$or");
                return this;
            }

            public QueryGenerator.IQueryReceiver OnNotConstraint()
            {
                Ops.Add("$not");
                return this;
            }

            public QueryGenerator.IQueryReceiver PushConstraint()
            {
                Ops.Add("$push");
                return this;
            }

            public QueryGenerator.IQueryReceiver PopConstraint()
            {
                Ops.Add("$pop");
                return this;
            }

            public QueryGenerator.IQueryReceiver OnSphere(IEnumerable<int> param)
            {
                Ops.Add($"$sphere({string.Join(",", param)})");
                return this;
            }

            public QueryGenerator.IQueryReceiver OnCylinder(IEnumerable<int> param)
            {
                Ops.Add($"$cylinder({string.Join(",", param)})");
                return this;
            }

            public QueryGenerator.IQueryReceiver OnBox(IEnumerable<int> param)
            {
                Ops.Add($"$box({string.Join(",", param)})");
                return this;
            }

            public QueryGenerator.IQueryReceiver OnEntityId(long param)
            {
                Ops.Add($"$entityid({param})");
                return this;
            }

            public QueryGenerator.IQueryReceiver OnComponentConstraint(string componentName)
            {
                Ops.Add($"$has_component({string.Join(",", componentName)})");
                return this;
            }

            public override string ToString()
            {
                return Ops.Any() ? Ops.Last() : "<empty>";
            }
        }
    }
}
