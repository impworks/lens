using System;
using System.Threading.Tasks;
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

        #region Rejections

        [Test]
        public void AwaitOutsideAFunctionIsRejected()
        {
            TestError("await 1", "LE3174");
        }

        [Test]
        public void UndeclaredReturnTypeIsRejected()
        {
            TestError(
                @"
fun broken ->
    await 1",
                "LE3175"
            );
        }

        [Test]
        public void AsyncVoidIsRejected()
        {
            TestError(
                @"
fun broken:int ->
    await 1",
                "LE3176"
            );
        }

        [Test]
        public void PureAsyncIsRejected()
        {
            TestError(
                @"
pure fun broken:Task<int> ->
    await 1",
                "LE3177"
            );
        }

        [Test]
        public void AwaitInsideAnExpressionIsRejected()
        {
            TestError(
                @"
fun broken:Task<int> (t:Task<int>) ->
    1 + (await t)",
                "LE3179"
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

        private static void TestAsyncConfigured(Action<LensCompiler> setup, string src, object expected, Func<object, object> project)
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            setup(compiler);
            Assert.AreEqual(expected, project(compiler.Run(src)));
        }

        #endregion
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
