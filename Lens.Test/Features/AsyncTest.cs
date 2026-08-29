using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lens.Compiler;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class AsyncTest : TestBase
    {
        #region Awaiting

        [Test]
        public void AwaitATaskWithAResult()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    await (delay 21)

fetch ()",
                42
            );
        }

        [Test]
        public void AwaitInTheMiddleOfABody()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    var total = 0
    var one = await (delay 1)
    total = total + one
    var two = await (delay 2)
    total + two

fetch ()",
                6
            );
        }

        [Test]
        public void AwaitInsideALoop()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    var total = 0
    for i in 1..5 do
        var step = await (delay i)
        total = total + step
    total

fetch ()",
                20
            );
        }

        [Test]
        public void AwaitInsideACondition()
        {
            TestAsync(
                @"
fun fetch:Task<int> (flag:bool) ->
    var result = 0
    if flag then
        result = await (delay 10)
    else
        result = await (delay 100)
    result

fetch true",
                20
            );
        }

        [Test]
        public void AwaitedResultCanBeDiscarded()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    await (delay 1)
    await (delay 2)
    3

fetch ()",
                3
            );
        }

        [Test]
        public void AssignmentFromAnAwait()
        {
            TestAsync(
                @"
fun fetch:Task<int> ->
    var value = 0
    value = await (delay 20)
    value

fetch ()",
                40
            );
        }

        [Test]
        public void ArgumentsSurviveTheSuspension()
        {
            TestAsync(
                @"
fun fetch:Task<int> (a:int b:int) ->
    let first = await (delay a)
    let second = await (delay b)
    first + second

fetch 5 10",
                30
            );
        }

        [Test]
        public void ATaskWithoutAResult()
        {
            TestConfigured(
                Setup,
                @"
fun run:Task (n:int) ->
    await (delay n)
    ()

var t = run 5
t.Wait ()
t.IsCompleted",
                true
            );
        }

        #endregion

        #region The awaiter pattern

        /// <summary>
        /// Nothing here knows what a Task is. The awaiter is found by its shape, so a host type
        /// that has never heard of the compiler works the same way.
        /// </summary>
        [Test]
        public void AHostDefinedAwaitable()
        {
            TestConfigured(
                c =>
                    {
                        c.RegisterFunction("custom", new Func<int, Awaitable>(value => new Awaitable(value)));
                        c.RegisterFunction("text", (Task<string> task) => task.Result);
                    },
                @"
fun fetch:Task<string> ->
    let value = await (custom 7)
    ""got "" + value

text (fetch ())",
                "got 7"
            );
        }

#if NET_CORE

        [Test]
        public void AValueTask()
        {
            TestConfigured(
                c =>
                    {
                        c.RegisterFunction("valued", (int value) => new ValueTask<int>(value * 3));
                        c.RegisterFunction("Wait", (Task<int> task) => task.Result);
                    },
                @"
fun fetch:Task<int> ->
    await (valued 4)

Wait (fetch ())",
                12
            );
        }

#endif

        #endregion

        #region Exceptions

        [Test]
        public void AnExceptionOnTheSynchronousPathReachesTheTask()
        {
            TestConfigured(
                Setup,
                @"
fun fetch:Task<int> (flag:bool) ->
    if flag then
        throw new System.InvalidOperationException ""boom""
    await (delay 1)

Failure (fetch true)",
                "boom"
            );
        }

        [Test]
        public void AnExceptionAfterASuspensionReachesTheTask()
        {
            TestConfigured(
                Setup,
                @"
fun fetch:Task<int> ->
    let value = await (delay 1)
    throw new System.InvalidOperationException ""late""

Failure (fetch ())",
                "late"
            );
        }

        [Test]
        public void AFailedAwaitPropagates()
        {
            TestConfigured(
                Setup,
                @"
fun fetch:Task<int> ->
    await (explode 0)

Failure (fetch ())",
                "inner"
            );
        }

        #endregion

        #region Host interop

        [Test]
        public void TheTaskCanBeAwaitedByTheHost()
        {
            TestAsyncConfigured(
                Setup,
                @"
fun fetch:Task<int> ->
    let a = await (delay 3)
    let b = await (delay 4)
    a + b

fetch ()",
                14,
                result => ((Task<int>) result).Result
            );
        }

        #endregion

        #region Generics

        [Test]
        public void GenericAsyncFunction()
        {
            TestAsyncConfigured(
                Setup,
                @"
fun twice<T>:Task<T> (item:T task:Task<int>) ->
    let ignored = await task
    item

twice 21 (delay 1)",
                21,
                result => ((Task<int>) result).Result
            );
        }

        [Test]
        public void GenericAsyncOverTheAwaitedValue()
        {
            TestAsyncConfigured(
                Setup,
                @"
fun unwrap<T>:Task<T> (source:Task<T>) ->
    await source

unwrap (delay 5)",
                10,
                result => ((Task<int>) result).Result
            );
        }

        #endregion

        #region Top level

        [Test]
        public void TopLevelAwaitProducesTheScriptValue()
        {
            TestTopLevel("await (delay 21)", 42);
        }

        [Test]
        public void TopLevelAwaitOfATaskWithoutAResultProducesNull()
        {
            // the script's last statement has no value, and the script still answers something
            TestTopLevel(
                @"
use System.Threading.Tasks
await (Task::Delay 10)",
                null
            );
        }

        [Test]
        public void TopLevelAwaitInTheMiddleOfAScript()
        {
            TestTopLevel(
                @"
var total = 0
var one = await (delay 1)
total = total + one
var two = await (delay 2)
total + two",
                6
            );
        }

        [Test]
        public void TopLevelAwaitInsideALoop()
        {
            TestTopLevel(
                @"
var total = 0
for i in 1..5 do
    var step = await (delay i)
    total = total + step
total",
                20
            );
        }

        [Test]
        public void TopLevelAwaitInsideACondition()
        {
            TestTopLevel(
                @"
var flag = true
if flag then
    await (delay 3)
else
    0",
                6
            );
        }

        [Test]
        public void TopLevelAwaitOfAValueThatIsNotATask()
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            compiler.RegisterFunction("custom", new Func<int, Awaitable>(value => new Awaitable(value)));

            // the custom awaiter answers a string, which is what the script answers in turn
            Assert.AreEqual("21", compiler.RunAsync("await (custom 21)").Result);
        }

        [Test]
        public void TopLevelAwaitCallsAnAsyncFunction()
        {
            TestTopLevel(
                @"
fun fetch:Task<int> (value:int) ->
    await (delay value)

await (fetch 4)",
                8
            );
        }

        [Test]
        public void TopLevelAwaitDoesNotDisturbAScriptThatOnlyReturnsATask()
        {
            // a script that hands out a task is not a script that waits for one: this one still
            // answers through the synchronous door, and answers the task itself
            var compiler = CreateCompiler(new LensCompilerOptions());
            Setup(compiler);

            var result = compiler.Run(
                @"
fun fetch:Task<int> ->
    await (delay 21)

fetch ()"
            );

            Assert.IsInstanceOf<Task<int>>(result);
            Assert.AreEqual(42, ((Task<int>) result).Result);
        }

        [Test]
        public void TopLevelAwaitIsNotAProblemForAnEditor()
        {
            // the analysis an editor runs stops short of emission, and used to report the await it
            // found at the top level as an error
            var ctx = new Context(new LensCompilerOptions());
            ctx.Analyze(Parse("use System.Threading.Tasks\nawait (Task::Delay 10)"));

            CollectionAssert.IsEmpty(ctx.Diagnostics.Select(x => x.Message).ToArray());
        }

#if NET_CLASSIC
        [Test]
        public void TopLevelAwaitInASavedAssembly()
        {
            // nothing is registered here, and deliberately: an assembly that is to be saved cannot
            // import anything from the host, so the script has to await something of its own
            var compiler = CreateCompiler(new LensCompilerOptions {AllowSave = true});

            Assert.AreEqual(
                21,
                compiler.Run(
                    @"
use System.Threading.Tasks
await (Task::Delay 10)
21"
                )
            );
        }
#endif

        #endregion

        #region The two doors

        [Test]
        public void ASynchronousScriptAnsweringThroughTheAsynchronousDoor()
        {
            var task = RunAsyncScript(null, "1 + 2");

            // it had nothing to wait for, so it ran before the task was handed back
            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual(3, task.Result);
        }

        [Test]
        public void AnAsynchronousScriptAnsweringThroughTheSynchronousDoor()
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            Setup(compiler);

            Assert.AreEqual(42, compiler.Run("await (delay 21)"));
        }

        [Test]
        public void ASynchronousScriptThatThrowsFaultsTheTask()
        {
            var task = RunAsyncScript(null, @"throw new System.InvalidOperationException ""sync""");

            Assert.IsTrue(task.IsFaulted);
            Assert.AreEqual("sync", task.Exception.GetBaseException().Message);
        }

        [Test]
        public void AnAsynchronousScriptThatThrowsFaultsTheTask()
        {
            var task = RunAsyncScript(Setup, @"
let value = await (delay 1)
throw new System.InvalidOperationException ""async""");

            Assert.AreEqual("async", WaitForFailure(task));
        }

        [Test]
        public void AFailedTopLevelAwaitFaultsTheTask()
        {
            Assert.AreEqual("inner", WaitForFailure(RunAsyncScript(Setup, "await (explode 0)")));
        }

        [Test]
        public void AFailedTopLevelAwaitThrowsThroughTheSynchronousDoor()
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            Setup(compiler);

            // unwrapped: the sync door reports what the script threw, not an AggregateException
            var error = Assert.Throws<InvalidOperationException>(() => compiler.Run("await (explode 0)"));
            Assert.AreEqual("inner", error.Message);
        }

        [Test]
        public void TheSynchronousDoorRefusesToBlockUnderASynchronizationContext()
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            var pending = new TaskCompletionSource<int>();
            compiler.RegisterFunction("pending", new Func<Task<int>>(() => pending.Task));

            var original = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DeadSynchronizationContext());

            try
            {
                // waiting here would post the continuation back to the thread doing the waiting
                Assert.Throws<InvalidOperationException>(() => compiler.Run("await (pending ())"));
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(original);
                pending.SetResult(0);
            }
        }

        [Test]
        public void TheSynchronousDoorStillAnswersUnderASynchronizationContextWhenNothingSuspends()
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            compiler.RegisterFunction("finished", new Func<Task<int>>(() => Task.FromResult(21)));

            var original = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DeadSynchronizationContext());

            try
            {
                // the machine never really suspended, so there is nothing to deadlock over
                Assert.AreEqual(21, compiler.Run("await (finished ())"));
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(original);
            }
        }

        #endregion

        #region Rejections

        [Test]
        public void RedefiningRunAsyncIsRejected()
        {
            TestError(
                @"
fun RunAsync -> 1

1",
                "LE3095"
            );
        }

        [Test]
        public void AwaitInsideATopLevelLambdaIsRejected()
        {
            TestError(
                @"
use System.Threading.Tasks
let f = ->
    await (Task::Delay 1)
f ()",
                "LE3171"
            );
        }

        // the rejections below are reported at a part of the declaration rather than at the whole
        // of it, so each of them asserts where it lands

        [Test]
        public void UndeclaredReturnTypeIsRejected()
        {
            TestErrorAt(
                @"
fun broken ->
    await 1",
                "LE3175",
                2, 5, 2, 11
            );
        }

        [Test]
        public void AsyncVoidIsRejected()
        {
            TestErrorAt(
                @"
fun broken:int ->
    await 1",
                "LE3176",
                2, 12, 2, 15
            );
        }

        [Test]
        public void PureAsyncIsRejected()
        {
            TestErrorAt(
                @"
pure fun broken:Task<int> ->
    await 1",
                "LE3177",
                2, 10, 2, 16
            );
        }

        [Test]
        public void ABrokenAwaitedExpressionIsReportedOnce()
        {
            // the state machine turns the await into a handful of statements that all mention the
            // awaiter it declares, so a mistake in the awaited expression used to be followed by
            // one complaint per generated statement about a name the script never contained
            TestErrors(
                @"
use System.Threading.Tasks
fun broken:Task (time:int) ->
    println ""before""
    await (Task::Delay ""x"")
    println ""after""

1",
                CompilerMessages.TypeMethodArgumentsMismatch
            );
        }

        [Test]
        public void AwaitingSomethingThatIsNotAwaitableIsRejected()
        {
            // the state machine asks the expression for an awaiter, and letting that call report
            // its own failure describes the mistake as a missing GetAwaiter - a name the script
            // never contained, at a place it was never written
            TestErrorAt(
                "await 10",
                "LE3206",
                1, 7, 1, 9
            );
        }

        [Test]
        public void AResultOfTheWrongTypeIsReportedAtTheExpression()
        {
            // the result is handed to a completion source, and letting that call report the
            // mismatch described it as a missing overload of TaskCompletionSource - at the first
            // lexem of the file, since the call is not something the script contains
            TestErrorAt(
                @"
use System.Threading.Tasks
fun fetch:Task<int> ->
    await (Task::Delay 1)
    ""x""",
                "LE3061",
                5, 5, 5, 8
            );
        }

        [Test]
        public void AwaitInsideLambdaIsRejected()
        {
            TestError(
                @"
fun broken:Task<int> (t:Task<int>) ->
    let f = ->
        await t
    1",
                "LE3171"
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

            compiler.RegisterFunction("explode", new Func<int, Task<int>>(_ => Task.Run(new Func<int>(() =>
                        {
                            Task.Delay(5).Wait();
                            throw new InvalidOperationException("inner");
                        }
                    )
                )
            ));

            compiler.RegisterFunction("Wait", (Task<int> task) => task.Result);
            compiler.RegisterFunction("Failure", (Task<int> task) =>
                {
                    try
                    {
                        task.Wait();
                        return "no error";
                    }
                    catch (AggregateException ex)
                    {
                        return ex.GetBaseException().Message;
                    }
                }
            );
        }

        private static void TestAsync(string src, object expected)
        {
            TestAsyncConfigured(Setup, src, expected, result => ((Task<int>) result).Result);
        }

        /// <summary>
        /// Checks the value a script that awaits at its top level answers with, through the door
        /// meant for it.
        /// </summary>
        private static void TestTopLevel(string src, object expected)
        {
            Assert.AreEqual(expected, RunAsyncScript(Setup, src).Result);
        }

        private static Task<object> RunAsyncScript(Action<LensCompiler> setup, string src)
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            setup?.Invoke(compiler);

            return compiler.RunAsync(src);
        }

        /// <summary>
        /// The message of whatever a script failed with, waited for rather than polled.
        /// </summary>
        private static string WaitForFailure(Task<object> task)
        {
            try
            {
                task.Wait();
                return "no error";
            }
            catch (AggregateException ex)
            {
                return ex.GetBaseException().Message;
            }
        }

        private static void TestAsyncConfigured(Action<LensCompiler> setup, string src, object expected, Func<object, object> project)
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            setup(compiler);
            Assert.AreEqual(expected, project(compiler.Run(src)));
        }

        #endregion
    }

    /// <summary>
    /// A context that accepts continuations and never runs them, which is what a UI thread blocked
    /// on a task amounts to.
    /// </summary>
    internal class DeadSynchronizationContext : SynchronizationContext
    {
        public override void Post(SendOrPostCallback d, object state)
        {
        }

        public override void Send(SendOrPostCallback d, object state)
        {
        }
    }

    /// <summary>
    /// An awaitable that is nothing to do with Task: it only has the four members the pattern asks
    /// for. It is public and top-level because the script is compiled into an assembly of its own,
    /// which is not a friend of this one.
    /// </summary>
    public class Awaitable
    {
        private readonly int _value;

        public Awaitable(int value)
        {
            _value = value;
        }

        public Awaitable GetAwaiter()
        {
            return this;
        }

        public bool IsCompleted => true;

        public void OnCompleted(Action continuation)
        {
            continuation();
        }

        public string GetResult()
        {
            return _value.ToString();
        }
    }
}
