using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Lens.Analysis;
using Lens.Compiler;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// Phase 5: a lambda passed where an Expression&lt;TDelegate&gt; is wanted becomes a tree rather
    /// than a delegate, so that a LINQ call binds to Queryable instead of Enumerable.
    ///
    /// The trees are asserted against the string form of the tree the C# compiler builds for the
    /// same lambda - that is what "matches what C# produces" can be checked as without reimplementing
    /// tree comparison - and then actually executed through a real provider.
    /// </summary>
    [TestFixture]
    internal class ExpressionTreeTest : TestBase
    {
        #region Overload selection

        [Test]
        public void QueryableOverloadIsPreferredOverEnumerable()
        {
            var query = Query(@"users
    |> Where u -> u.Age > 18");

            var call = (MethodCallExpression) query.Expression;
            Assert.AreEqual(typeof(Queryable), call.Method.DeclaringType, "Resolved to {0}", call.Method.DeclaringType);
        }

        [Test]
        public void EnumerableOverloadIsUsedForASequence()
        {
            // the same script against IEnumerable has no Queryable overload to bind to, and the
            // lambda goes back to being an ordinary delegate
            Test(
                @"new [1; 2; 30]
    |> Where x -> x > 18
    |> Count ()",
                1
            );
        }

        [Test]
        public void ExplicitlyTypedLambdaAlsoBecomesATree()
        {
            var query = Query(@"users
    |> Where (u:Lens.Test.Features.TreeUser) -> u.Age > 18");

            var call = (MethodCallExpression) query.Expression;
            Assert.AreEqual(typeof(Queryable), call.Method.DeclaringType);
        }

        #endregion

        #region Tree shape

        [Test]
        public void Comparison()
        {
            AssertPredicate("u -> u.Age >= 18", u => u.Age >= 18);
            AssertPredicate("u -> u.Age <> 18", u => u.Age != 18);
            AssertPredicate("u -> u.Name == \"bob\"", u => u.Name == "bob");
        }

        [Test]
        public void LogicalOperators()
        {
            AssertPredicate("u -> u.Age >= 18 && u.Age < 65", u => u.Age >= 18 && u.Age < 65);
            AssertPredicate("u -> u.Age < 18 || u.Age > 65", u => u.Age < 18 || u.Age > 65);
            AssertPredicate("u -> not (u.Age > 18)", u => !(u.Age > 18));
        }

        [Test]
        public void MemberAccess()
        {
            AssertPredicate("u -> u.Manager.Age > 18", u => u.Manager.Age > 18);
            AssertPredicate("u -> u.Tag > 0", u => u.Tag > 0);
        }

        [Test]
        public void MethodCall()
        {
            AssertPredicate("u -> u.Name.StartsWith \"b\"", u => u.Name.StartsWith("b"));
            AssertPredicate("u -> u.Name.Length > 2", u => u.Name.Length > 2);
        }

        [Test]
        public void Arithmetic()
        {
            AssertProjection("u -> u.Age * 2 + 1", u => u.Age * 2 + 1);
        }

        [Test]
        public void NullComparison()
        {
            AssertPredicate("u -> u.Manager <> null", u => u.Manager != null);
        }

        [Test]
        public void Nullables()
        {
            // the tree lifts the comparison itself, so a null Bonus makes the predicate false rather
            // than throwing - which is the C# semantic, and the one three-valued SQL agrees with
            AssertPredicate("u -> u.Bonus > 5", u => u.Bonus > 5);
            AssertPredicate("u -> u.Bonus == null", u => u.Bonus == null);

            Assert.AreEqual(new[] {"alice"}, Names(@"users
    |> Where u -> u.Bonus > 5
    |> Select u -> u.Name
    |> ToArray ()"));

            Assert.AreEqual(new[] {"bob"}, Names(@"users
    |> Where u -> u.Bonus == null
    |> Select u -> u.Name
    |> ToArray ()"));
        }

        [Test]
        public void NullableArithmetic()
        {
            AssertNullableProjection("u -> u.Bonus + 1", u => u.Bonus + 1);
        }

        [Test]
        public void ExtensionMethodInsideTheTree()
        {
            // an extension method resolves through a different path than an ordinary call, and has
            // to end up in the tree as the static call it really is
            AssertPredicate("u -> u.Name.Count () > 2", u => u.Name.Count() > 2);
        }

        [Test]
        public void ArrayAccess()
        {
            AssertPredicate("u -> u.Tags[0] > 0", u => u.Tags[0] > 0);
            AssertPredicate("u -> u.Tags.Length > 1", u => u.Tags.Length > 1);
        }

        [Test]
        public void Indexer()
        {
            AssertPredicate("u -> u.Meta[\"rank\"] > 0", u => u.Meta["rank"] > 0);
        }

        [Test]
        public void Coalesce()
        {
            AssertProjection("u -> u.Bonus ?? 0", u => u.Bonus ?? 0);
        }

        [Test]
        public void CastAndTypeCheck()
        {
            AssertPredicate("u -> (u.Age as double) > 18.5", u => (double) u.Age > 18.5);
            AssertPredicate("u -> u.Manager is Lens.Test.Features.TreeUser", u => u.Manager is TreeUser);
        }

        [Test]
        public void Conditional()
        {
            AssertProjection("u -> (if u.Age > 18 then 1 else 0)", u => u.Age > 18 ? 1 : 0);
        }

        [Test]
        public void StringConcatenation()
        {
            // LENS expands '+' over strings into a call to string.Concat, which is the very shape
            // the C# compiler puts into a tree
            AssertStringProjection("u -> u.Name + \"!\"", u => u.Name + "!");
        }

        [Test]
        public void ProjectionToANewObject()
        {
            var query = Query(@"users
    |> Select u -> new Lens.Test.Features.TreeDto u.Name u.Age");

            var call = (MethodCallExpression) query.Expression;
            var lambda = (LambdaExpression) StripQuote(call.Arguments[1]);

            Expression<Func<TreeUser, TreeDto>> expected = u => new TreeDto(u.Name, u.Age);
            Assert.AreEqual(expected.ToString(), lambda.ToString());
            Assert.AreEqual(ExpressionType.New, lambda.Body.NodeType);
        }

        [Test]
        public void ProjectionToARecord()
        {
            // a record is field-only with a generated constructor, and that constructor is what the
            // tree names - the shape of the single most common real query
            var names = (IEnumerable<object>) Run(
                @"record Summary
    name : string
    age : int

users
    |> Select u -> new Summary u.Name u.Age
    |> Select s -> s.name
    |> ToArray ()"
            );

            Assert.AreEqual(new[] {"alice", "bob"}, names.Cast<string>().ToArray());
        }

        #endregion

        #region Captured variables

        [Test]
        public void CapturedLocalIsReadWhenTheQueryRuns()
        {
            // C# captures through the closure class, so a mutation after the query was built is
            // visible to it; a tree that baked the value in as a constant would not see this
            var result = Run(@"var limit = 100
var query = users.Where (u -> u.Age > limit)
limit = 18
query.Count ()");

            Assert.AreEqual(1, result);
        }

        [Test]
        public void CapturedLocalIsAField()
        {
            var query = Query(@"var limit = 18
users.Where (u -> u.Age > limit)");

            var call = (MethodCallExpression) query.Expression;
            var lambda = (LambdaExpression) StripQuote(call.Arguments[1]);
            var comparison = (BinaryExpression) lambda.Body;

            Assert.AreEqual(ExpressionType.MemberAccess, comparison.Right.NodeType);
            Assert.AreEqual(ExpressionType.Constant, ((MemberExpression) comparison.Right).Expression.NodeType);
        }

        #endregion

        #region Execution against a provider

        [Test]
        public void QueryIsExecuted()
        {
            Assert.AreEqual(
                new[] {"alice"},
                Names(@"users
    |> Where u -> u.Age > 18
    |> Select u -> u.Name
    |> ToArray ()")
            );
        }

        [Test]
        public void OrderingIsExecuted()
        {
            Assert.AreEqual(
                new[] {"alice", "bob"},
                Names(@"users
    |> OrderBy u -> u.Name
    |> Select u -> u.Name
    |> ToArray ()")
            );
        }

        [Test]
        public void ChainedQueryStaysQueryable()
        {
            var query = Query(@"users
    |> Where u -> u.Age > 18
    |> OrderBy u -> u.Name");

            var call = (MethodCallExpression) query.Expression;
            Assert.AreEqual("OrderBy", call.Method.Name);
            Assert.AreEqual(typeof(Queryable), call.Method.DeclaringType);
            Assert.AreEqual(typeof(Queryable), ((MethodCallExpression) call.Arguments[0]).Method.DeclaringType);
        }

        #endregion

        #region Rejected constructs

        [Test]
        public void BlockBodyIsRejected()
        {
            TestQueryError(
                @"users.Where
    <| u ->
        let x = u.Age
        x > 18",
                CompilerMessages.ExpressionTreeBlockBody
            );
        }

        [Test]
        public void SafeNavigationIsRejected()
        {
            TestQueryError(
                @"users
    |> Where u -> u.Manager?.Age > 18",
                CompilerMessages.ExpressionTreeNullSafe
            );
        }

        [Test]
        public void MatchBodyIsRejected()
        {
            // a match is a block, and the block form is what the author is told about
            TestQueryError(
                @"users.Where
    <| u ->
        match u.Age with
            case 18 then true
            case _ then false",
                CompilerMessages.ExpressionTreeBlockBody
            );
        }

        [Test]
        public void NestedLambdaIsRejected()
        {
            TestQueryError(
                @"users
    |> Where u -> ((x:int) -> x > 18) u.Age",
                CompilerMessages.ExpressionTreeUnsupportedNode
            );
        }

        [Test]
        public void UntranslatableOperatorIsRejected()
        {
            // string ordering goes through a helper in the IL backend and has no tree form
            TestQueryError(
                @"users
    |> Where u -> u.Name < ""b""",
                CompilerMessages.ExpressionTreeUnsupportedOperator
            );
        }

        [Test]
        public void DelegateValueIsRejected()
        {
            TestQueryError(
                @"let predicate = (u:Lens.Test.Features.TreeUser) -> u.Age > 18
users
    |> Where predicate",
                CompilerMessages.ExpressionTreeNoDelegateValue
            );
        }

        [Test]
        public void RejectedConstructsAreReportedWithoutEmitting()
        {
            // the tree builder is the emission half of a lambda body, so everything it rejects used
            // to be invisible to an editor - which binds a script and never emits it. The same walk
            // now runs while binding, and has to reach the same verdicts.
            AssertAnalysisReports(
                @"users.Where
    <| u ->
        let x = u.Age
        x > 18",
                CompilerMessages.ExpressionTreeBlockBody
            );

            AssertAnalysisReports(
                @"users
    |> Where u -> u.Manager?.Age > 18",
                CompilerMessages.ExpressionTreeNullSafe
            );

            AssertAnalysisReports(
                @"users
    |> Where u -> ((x:int) -> x > 18) u.Age",
                CompilerMessages.ExpressionTreeUnsupportedNode
            );

            AssertAnalysisReports(
                @"users
    |> Where u -> u.Name < ""b""",
                CompilerMessages.ExpressionTreeUnsupportedOperator
            );
        }

        [Test]
        public void ATranslatableQueryIsAnalysedWithoutComplaint()
        {
            // the check above is worth nothing if the dry run reports the queries that do work
            AssertAnalysisReports(
                @"users
    |> Where u -> u.Age > 18
    |> Select u -> u.Name",
                null
            );
        }

        #endregion

        #region Safe mode

        [Test]
        public void SafeModeCoversTheTreeType()
        {
            // the tree's own type goes through the same check as any other, so a script cannot
            // reach System.Linq.Expressions through a query while the restrictions forbid it
            var options = new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitNamespaces = new List<string> {"System.Linq.Expressions"}
            };

            var exception = Assert.Throws<LensCompilerException>(() => Run(@"users
    |> Where u -> u.Age > 18", options));

            Assert.AreEqual("LE3097", exception.Message.Substring(0, 6));
        }

        #endregion

        #region Helpers

        private static readonly TreeUser[] Users =
        {
            new TreeUser {Name = "alice", Age = 30, Tag = 1, Bonus = 10},
            new TreeUser {Name = "bob", Age = 10, Tag = 0, Bonus = null}
        };

        private static object Run(string src, LensCompilerOptions options = null)
        {
            var compiler = CreateCompiler(options ?? new LensCompilerOptions {UnrollConstants = true});
            compiler.RegisterProperty("users", () => Users.AsQueryable());
            return compiler.Run(src);
        }

        private static IQueryable Query(string src)
        {
            return (IQueryable) Run(src);
        }

        private static string[] Names(string src)
        {
            return (string[]) Run(src);
        }

        /// <summary>
        /// Checks what the editor makes of a query: the analyzer binds the script and never emits
        /// it, so a diagnostic it does not report is one the author never sees.
        /// </summary>
        private static void AssertAnalysisReports(string src, string message)
        {
            const string declaration = @"declare
    let users : System.Linq.IQueryable<Lens.Test.Features.TreeUser>

";

            var analyzer = new ScriptAnalyzer();
            analyzer.AddReference(typeof(TreeUser).Assembly);

            using (var analysis = analyzer.Analyze(declaration + src))
            {
                var actual = analysis.Diagnostics.Select(x => x.Message).ToArray();

                if (message == null)
                {
                    Assert.IsEmpty(actual, "Expected no diagnostics, got: {0}", string.Join(" | ", actual));
                    return;
                }

                Assert.IsTrue(
                    actual.Any(x => x.StartsWith(message.Substring(0, 6))),
                    "Expected {0}, got: {1}",
                    message,
                    string.Join(" | ", actual)
                );
            }
        }

        private static void TestQueryError(string src, string message)
        {
            var exception = Assert.Throws<LensCompilerException>(() => Run(src));
            Assert.AreEqual(
                message.Substring(0, 6),
                exception.Message.Substring(0, 6),
                "Message does not match!\nExpected: {0}\nActual: {1}",
                message,
                exception.Message
            );
        }

        /// <summary>
        /// Compares the tree LENS builds for a predicate against the one C# builds for the same one.
        /// </summary>
        /// <summary>
        /// A call in a tree that leaves its trailing arguments out carries the defaults as
        /// constants, exactly as the one C# builds for the same call does.
        /// </summary>
        [Test]
        public void CallWithOmittedArgumentsBecomesATree()
        {
            AssertStringProjection(
                "u -> Lens.Test.Internals.Optionals::Opt u.Age",
                u => Internals.Optionals.Opt(u.Age, 5, "z")
            );
        }

        private static void AssertPredicate(string lens, Expression<Func<TreeUser, bool>> expected)
        {
            AssertLambda("Where", lens, expected);
        }

        private static void AssertProjection(string lens, Expression<Func<TreeUser, int>> expected)
        {
            AssertLambda("Select", lens, expected);
        }

        private static void AssertStringProjection(string lens, Expression<Func<TreeUser, string>> expected)
        {
            AssertLambda("Select", lens, expected);
        }

        private static void AssertNullableProjection(string lens, Expression<Func<TreeUser, int?>> expected)
        {
            AssertLambda("Select", lens, expected);
        }

        private static void AssertLambda(string method, string lens, LambdaExpression expected)
        {
            var result = Run($"users{Environment.NewLine}    |> {method} {lens}");
            var call = (MethodCallExpression) ((IQueryable) result).Expression;
            var actual = (LambdaExpression) StripQuote(call.Arguments[1]);

            Assert.AreEqual(expected.ToString(), actual.ToString());
        }

        private static Expression StripQuote(Expression node)
        {
            return node is UnaryExpression unary && unary.NodeType == ExpressionType.Quote ? unary.Operand : node;
        }

        #endregion
    }

    public class TreeUser
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public int Tag;
        public int[] Tags = {1, 2};
        public Dictionary<string, int> Meta { get; set; } = new Dictionary<string, int> {{"rank", 1}};
        public int? Bonus { get; set; }
        public TreeUser Manager { get; set; }
    }

    public class TreeDto
    {
        public TreeDto(string name, int age)
        {
            Name = name;
            Age = age;
        }

        public string Name { get; }
        public int Age { get; }
    }
}
