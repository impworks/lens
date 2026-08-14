using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lens.SyntaxTree;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// Suspending in the middle of an expression.
    ///
    /// An await has to end up a statement, because a resume point is a place the machine leaves
    /// from and comes back to and a half-evaluated expression's stack does not survive the trip. An
    /// await written inside an expression is therefore lifted out of it, and everything the
    /// expression would have evaluated before reaching it is lifted out with it - in order, and
    /// into names - so that what ran before the suspension still does.
    /// </summary>
    [TestFixture]
    internal class StateMachineSpillingTest : TestBase
    {
        #region Arithmetic

        [Test]
        public void AwaitAsAnOperand()
        {
            TestAsync(
                @"
fun fetch:Task<int> (t:Task<int>) ->
    1 + (await t)

fetch (delay 20)",
                41
            );
        }

        [Test]
        public void TwoAwaitsInOneExpression()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    (await (delay 1)) + (await (delay 2))

fetch ()",
                6
            );
        }

        [Test]
        public void AwaitNestedInsideAnAwait()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    await (delay (await (delay 3)))

fetch ()",
                12
            );
        }

        [Test]
        public void AwaitUnderAUnaryOperator()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    -(await (delay 5))

fetch ()",
                -10
            );
        }

        #endregion

        #region Evaluation order

        /// <summary>
        /// The decisive one. Marking "a" is written before the await, so it has to run before it -
        /// which it only does if the rewrite evaluates it into a name on the way past. Lifting the
        /// await out without doing that would run the awaited call, and the marking inside it,
        /// first.
        /// </summary>
        [Test]
        public void WhatPrecedesAnAwaitStillPrecedesIt()
        {
            var order = new StringBuilder();

            TestOrdered(
                order,
                @"
fun fetch:Task<int> ->
    (mark ""a"") + (await (delay (mark ""b"")))

fetch ()",
                3,
                "ab"
            );
        }

        [Test]
        public void ArgumentsAreEvaluatedLeftToRightAroundASuspension()
        {
            var order = new StringBuilder();

            TestOrdered(
                order,
                @"
fun add3:int (a:int b:int c:int) -> a + b + c

fun fetch:Task<int> ->
    add3 (mark ""a"") (await (delay (mark ""b""))) (mark ""c"")

fetch ()",
                4,
                "abc"
            );
        }

        [Test]
        public void EachSuspensionKeepsItsPlaceAmongTheOthers()
        {
            var order = new StringBuilder();

            TestOrdered(
                order,
                @"
fun add3:int (a:int b:int c:int) -> a + b + c

fun fetch:Task<int> ->
    add3 (await (delay (mark ""a""))) (mark ""b"") (await (delay (mark ""c"")))

fetch ()",
                5,
                "abc"
            );
        }

        #endregion

        #region Calls, members and indexers

        [Test]
        public void AwaitAsTheReceiverOfACall()
        {
            TestAsync(
                @"
fun fetch:Task<string> ->
    (await (delay 21)).ToString ()

fetch ()",
                "42",
                result => ((Task<string>) result).Result
            );
        }

        [Test]
        public void AwaitAsAnArgumentOfAMethodCall()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    let list = new List<int> ()
    list.Add (await (delay 3))
    list.Add (await (delay 4))
    list[0] * 100 + list[1]

fetch ()",
                608
            );
        }

        [Test]
        public void AwaitInsideAnIndex()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    let data = new[10; 20; 30; 40]
    data[await (delay 1)]

fetch ()",
                30
            );
        }

        [Test]
        public void AwaitAsTheValueOfAnIndexedAssignment()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    let data = new[0; 0; 0]
    data[1] = await (delay 7)
    data[1]

fetch ()",
                14
            );
        }

        #endregion

        #region Collections

        [Test]
        public void AwaitInsideAnArrayLiteral()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    let data = new[1; await (delay 2); 3]
    data[0] + data[1] + data[2]

fetch ()",
                8
            );
        }

        [Test]
        public void AwaitInsideATuple()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    let pair = new(await (delay 1); await (delay 2))
    pair.Item1 + pair.Item2

fetch ()",
                6
            );
        }

        [Test]
        public void AwaitInsideADictionary()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    let map = new { 1 => await (delay 5); 2 => 7 }
    map[1] + map[2]

fetch ()",
                17
            );
        }

        #endregion

        #region Control structures

        [Test]
        public void AwaitInsideAnIfCondition()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    var result = 0
    if (await (delay 5)) > 9 then
        result = 1
    else
        result = 2
    result

fetch ()",
                1
            );
        }

        [Test]
        public void AwaitInsideAWhileConditionSuspendsOnEveryTurn()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    var total = 0
    while (await (delay total)) < 8 do
        total = total + 3
    total

fetch ()",
                6
            );
        }

        [Test]
        public void AwaitInsideTheSequenceOfAForeach()
        {
            TestAsync(
                @"
fun items:Task<IEnumerable<int>> (t:Task<IEnumerable<int>>) -> t

fun fetch:Task<int> (t:Task<IEnumerable<int>>) ->
    var total = 0
    for x in await t do
        total = total + x
    total

fetch (numbers ())",
                6
            );
        }

        [Test]
        public void AwaitInsideTheSubjectOfAMatch()
        {
            TestAsync(
                @"
fun fetch:Task<string> ->
    var result = """"
    match await (delay 1) with
        case 2 then result = ""two""
        case _ then result = ""other""
    result

fetch ()",
                "two",
                result => ((Task<string>) result).Result
            );
        }

        [Test]
        public void AwaitInsideACast()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    ((await (delay 5)) as object) as int

fetch ()",
                10
            );
        }

        #endregion

        #region Constructs that branch

        [Test]
        public void AwaitInsideABranchOfAValuedIf()
        {
            TestAsync(
                @"
fun fetch:Task<int> (n:int) ->
    let value = if n > 0 then await (delay n) else 0
    value + 1

fetch 5",
                11
            );
        }

        [Test]
        public void AValuedIfSuspendsInEitherBranch()
        {
            TestAsync(
                @"
fun fetch:Task<int> (n:int) ->
    100 + (if n > 0 then await (delay 1) else await (delay 7))

fetch 0",
                114
            );
        }

        /// <summary>
        /// The branch that was not taken must not be waited on - which is the whole difference
        /// between a construct that branches and one that does not.
        /// </summary>
        [Test]
        public void OnlyTheChosenBranchSuspends()
        {
            var order = new StringBuilder();

            TestOrdered(
                order,
                @"
fun fetch:Task<int> ->
    if false then
        await (delay (mark ""untaken""))
    else
        await (delay (mark ""taken""))

fetch ()",
                10,
                "taken"
            );
        }

        [Test]
        public void AwaitOnTheRightOfAnAnd()
        {
            TestAsync(
                @"
fun fetch:Task<int> (n:int) ->
    var hit = 0
    if n > 0 && (await (delay n)) > 5 then
        hit = 1
    hit

fetch 4",
                1
            );
        }

        [Test]
        public void AnAndDoesNotSuspendOnceTheLeftHasDecided()
        {
            var order = new StringBuilder();

            TestOrdered(
                order,
                @"
fun fetch:Task<int> ->
    var hit = 0
    if false && (await (delay (mark ""right""))) > 0 then
        hit = 1
    hit + (mark ""after"")

fetch ()",
                5,
                "after"
            );
        }

        [Test]
        public void AnOrDoesNotSuspendOnceTheLeftHasDecided()
        {
            var order = new StringBuilder();

            TestOrdered(
                order,
                @"
fun fetch:Task<int> ->
    var hit = 0
    if true || (await (delay (mark ""right""))) > 0 then
        hit = 1
    hit + (mark ""after"")

fetch ()",
                6,
                "after"
            );
        }

        [Test]
        public void AwaitAsTheFallbackOfACoalesce()
        {
            TestAsync(
                @"
fun fetch:Task<string> (text:string) ->
    text ?? (await (label ()))

fetch null",
                "fallback",
                result => ((Task<string>) result).Result
            );
        }

        [Test]
        public void ACoalesceDoesNotSuspendWhenTheLeftIsThere()
        {
            var order = new StringBuilder();

            TestOrdered(
                order,
                @"
fun fetch:Task<int> ->
    let present = new Nullable<int> 4
    (present ?? (await (delay (mark ""fallback"")))) + (mark ""after"")

fetch ()",
                9,
                "after"
            );
        }

        #endregion

        #region Everywhere else an expression goes

        [Test]
        public void AwaitOnTheRightOfAShortAssignment()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    var total = 1
    total += await (delay 3)
    total

fetch ()",
                7
            );
        }

        [Test]
        public void AwaitInsideAnInterpolatedString()
        {
            TestAsync(
                @"
fun fetch:Task<string> ->
    $""value={await (delay 4)}""

fetch ()",
                "value=8",
                result => ((Task<string>) result).Result
            );
        }

        [Test]
        public void AwaitInsideAMatchGuard()
        {
            TestAsync(
                @"
fun fetch:Task<int> (n:int) ->
    var result = 0
    match n with
        case x:int when (await (delay x)) > 5 then result = 1
        case _ then result = 2
    result

fetch 4",
                1
            );
        }

        [Test]
        public void AwaitAsAnArgumentOfALambdaCall()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    let double = (x:int) -> x * 2
    double (await (delay 3))

fetch ()",
                12
            );
        }

        #endregion

        #region Exceptions

        /// <summary>
        /// A failed operation turns back into an exception where its result is read, and that is
        /// still true when the reading happens in the middle of an expression the rewrite built.
        /// </summary>
        [Test]
        public void AFailureInTheMiddleOfAnExpressionPropagates()
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            Setup(compiler);

            var task = (Task<int>) compiler.Run(@"
fun fetch:Task<int> ->
    1 + (await (explode ()))

fetch ()");

            var error = Assert.Throws<AggregateException>(() => task.Wait());
            Assert.AreEqual("inner", error.GetBaseException().Message);
        }

        [Test]
        public void AFailureInTheMiddleOfAnExpressionIsCaught()
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            Setup(compiler);

            var task = (Task<int>) compiler.Run(@"
fun fetch:Task<int> ->
    var total = 0
    try
        total = 1 + (await (explode ()))
    catch e:System.InvalidOperationException
        total = 100
    total

fetch ()");

            Assert.AreEqual(100, task.Result);
        }

        #endregion

        #region The tree

        /// <summary>
        /// The rewrite builds new nodes around the old ones - copies of the expressions it had to
        /// replace an operand in - and must not write into any of them. Compiling the same tree
        /// twice is what proves it: the second run sees exactly what the parser produced.
        /// </summary>
        [Test]
        public void SpillingLeavesTheTreeAsItWas()
        {
            const string script = @"
fun fetch:Task<int> (n:int) ->
    let value = if n > 0 then await (delay n) else 0
    (mark ""a"") + value + (await (delay (mark ""b"")))

fetch 3";

            var nodes = Parse(script).ToArray();
            var pristine = Parse(script).ToArray();

            var order = new StringBuilder();
            Assert.AreEqual(9, RunOrdered(order, nodes));
            Assert.AreEqual("ab", order.ToString());

            // NodeBase compares structurally, so this asserts that the tree still describes the
            // source it was parsed from
            Assert.AreEqual(pristine, nodes);

            var second = new StringBuilder();
            Assert.AreEqual(9, RunOrdered(second, nodes));
            Assert.AreEqual("ab", second.ToString());
        }

        #endregion

        #region Rejections

        /// <summary>
        /// The one position left. A chain answers with the default of its own type when a receiver
        /// is null, and that type is the chain's value type lifted so that it can also be null -
        /// something the pass cannot write down, and cannot leave to a name that has only ever held
        /// the value. Answering 0 where the chain answers null is worse than refusing.
        /// </summary>
        [Test]
        public void AwaitInsideANullSafeChainIsRejected()
        {
            TestError(
                @"
fun broken:System.Threading.Tasks.Task<string> (s:string t:System.Threading.Tasks.Task<int>) ->
    s?.Substring (await t)",
                "LE3182"
            );
        }

        [Test]
        public void AwaitOnTheRightOfAShortCircuitingAssignmentIsRejected()
        {
            TestError(
                @"
fun broken:System.Threading.Tasks.Task<bool> (t:System.Threading.Tasks.Task<bool>) ->
    var flag = false
    flag &&= await t
    flag",
                "LE3179"
            );
        }

        #endregion

        #region Helpers

        private static void Setup(LensCompiler compiler)
        {
            // a real suspension: the task is not finished when the machine first looks at it
            compiler.RegisterFunction("delay", new Func<int, Task<int>>(value => Task.Run(() =>
                        {
                            Task.Delay(5).Wait();
                            return value * 2;
                        }
                    )
                )
            );

            compiler.RegisterFunction("numbers", new Func<Task<System.Collections.Generic.IEnumerable<int>>>(
                    () => Task.Run(() => (System.Collections.Generic.IEnumerable<int>) new[] {1, 2, 3})
                )
            );

            compiler.RegisterFunction("explode", new Func<Task<int>>(() => Task.Run(new Func<int>(() =>
                        {
                            Task.Delay(5).Wait();
                            throw new InvalidOperationException("inner");
                        }
                    )
                )
            ));

            compiler.RegisterFunction("label", new Func<Task<string>>(() => Task.Run(() =>
                        {
                            Task.Delay(5).Wait();
                            return "fallback";
                        }
                    )
                )
            );
        }

        private static void TestAsync(string src, object expected)
        {
            TestAsync(src, expected, result => ((Task<int>) result).Result);
        }

        private static void TestAsync(string src, object expected, Func<object, object> project)
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            Setup(compiler);
            Assert.AreEqual(expected, project(compiler.Run(src)));
        }

        /// <summary>
        /// Runs a script whose every step announces itself, and checks both what it produced and
        /// the order in which it got there.
        /// </summary>
        private static void TestOrdered(StringBuilder order, string src, int expected, string expectedOrder)
        {
            Assert.AreEqual(expected, ((Task<int>) MarkingCompiler(order).Run(src)).Result);
            Assert.AreEqual(expectedOrder, order.ToString());
        }

        private static int RunOrdered(StringBuilder order, IEnumerable<NodeBase> nodes)
        {
            return ((Task<int>) MarkingCompiler(order).Run(nodes)).Result;
        }

        private static LensCompiler MarkingCompiler(StringBuilder order)
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            Setup(compiler);

            // returns the length of what it was given, so that a marking can stand in for a value
            compiler.RegisterFunction("mark", new Func<string, int>(name =>
                    {
                        order.Append(name);
                        return name.Length;
                    }
                )
            );

            return compiler;
        }

        #endregion
    }
}
