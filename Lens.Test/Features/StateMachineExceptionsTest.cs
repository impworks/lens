using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// Suspending inside a try.
    ///
    /// Nothing may branch into a protected region, and leaving one runs its finally handlers - so a
    /// state machine cannot simply resume where it stopped. The catch and finally bodies are moved
    /// out of the region and run after it, which is what lets them suspend as freely as any other
    /// code.
    /// </summary>
    [TestFixture]
    internal class StateMachineExceptionsTest : TestBase
    {
        #region Iterators

        [Test]
        public void YieldInsideTryWithCatch()
        {
            Test(
                @"
fun items:IEnumerable<string> ->
    try
        yield ""a""
        throw new System.InvalidOperationException ""boom""
    catch e:System.InvalidOperationException
        yield e.Message

items ()
  |> Aggregate (a:string b:string) -> a + "","" + b",
                "a,boom"
            );
        }

        [Test]
        public void YieldInsideTryWithFinally()
        {
            Test(
                @"
fun items:IEnumerable<string> ->
    var log = """"
    try
        yield ""a""
        yield ""b""
    finally
        log = ""done""
    yield log

items ()
  |> Aggregate (a:string b:string) -> a + "","" + b",
                "a,b,done"
            );
        }

        [Test]
        public void YieldInsideAFinallyBody()
        {
            Test(
                @"
fun items:IEnumerable<string> ->
    try
        yield ""body""
    finally
        yield ""cleanup""

items ()
  |> Aggregate (a:string b:string) -> a + "","" + b",
                "body,cleanup"
            );
        }

        [Test]
        public void TheFinallyRunsWhenTheBodyThrows()
        {
            Test(
                @"
fun items:IEnumerable<string> (fail:bool) ->
    var log = """"
    try
        try
            yield ""a""
            if fail then
                throw new System.InvalidOperationException ""boom""
            yield ""b""
        finally
            log = ""cleaned""
    catch e:System.InvalidOperationException
        yield ""caught""
    yield log

items true
  |> Aggregate (a:string b:string) -> a + "","" + b",
                "a,caught,cleaned"
            );
        }

        [Test]
        public void UsingInsideAnIterator()
        {
            TestConfigured(
                c =>
                    {
                        c.RegisterFunction("probe", new Func<List<string>, Probe>(log => new Probe(log)));
                        c.RegisterFunction("join", new Func<List<string>, string>(log => string.Join(",", log)));
                    },
                @"
fun items:IEnumerable<string> (log:System.Collections.Generic.List<string>) ->
    using p = probe log do
        yield ""one""
        yield ""two""

var log = new System.Collections.Generic.List<string> ()
var acc = """"
for x in items log do
    acc = acc + x + "",""
acc + join log",
                "one,two,disposed"
            );
        }

        [Test]
        public void RethrowFromAMovedHandler()
        {
            TestConfigured(
                c => c.RegisterFunction("collect", new Func<IEnumerable<string>, string>(items =>
                    {
                        try
                        {
                            return string.Join(",", items);
                        }
                        catch (InvalidOperationException ex)
                        {
                            return "escaped:" + ex.Message;
                        }
                    }
                )),
                @"
fun items:IEnumerable<string> ->
    try
        yield ""a""
        throw new System.InvalidOperationException ""boom""
    catch e:System.InvalidOperationException
        yield ""seen""
        throw e

collect (items ())",
                "escaped:boom");
        }

        [Test]
        public void NestedTriesInsideAnIterator()
        {
            Test(
                @"
fun items:IEnumerable<string> ->
    try
        try
            yield ""inner""
            throw new System.InvalidOperationException ""deep""
        finally
            yield ""inner-finally""
    catch e:System.InvalidOperationException
        yield ""outer-catch:"" + e.Message

items ()
  |> Aggregate (a:string b:string) -> a + "","" + b",
                "inner,inner-finally,outer-catch:deep"
            );
        }

        /// <summary>
        /// The consumer stops reading half-way, so the machine is never resumed - and the finally
        /// still has to run. Dispose tells the machine which way it is going and asks it to carry
        /// on; the unwinding does the rest.
        /// </summary>
        [Test]
        public void AnAbandonedIteratorStillRunsItsFinally()
        {
            TestConfigured(
                c =>
                    {
                        c.RegisterFunction("probe", new Func<List<string>, Probe>(log => new Probe(log)));
                        c.RegisterFunction("join", new Func<List<string>, string>(log => string.Join(",", log)));
                    },
                @"
fun items:IEnumerable<int> (log:System.Collections.Generic.List<string>) ->
    using p = probe log do
        yield 1
        yield 2
        yield 3

var log = new System.Collections.Generic.List<string> ()
let taken = items log
  |> Take 2
  |> Sum ()
taken.ToString () + "":"" + (join log)",
                "3:disposed"
            );
        }

        [Test]
        public void AnAbandonedIteratorUnwindsNestedFinallies()
        {
            TestConfigured(
                c =>
                    {
                        c.RegisterFunction("probe", new Func<List<string>, Probe>(log => new Probe(log)));
                        c.RegisterFunction("join", new Func<List<string>, string>(log => string.Join(",", log)));
                    },
                @"
fun items:IEnumerable<int> (log:System.Collections.Generic.List<string>) ->
    try
        try
            yield 1
            yield 2
        finally
            log.Add ""inner""
    finally
        log.Add ""outer""

var log = new System.Collections.Generic.List<string> ()
let taken = items log
  |> Take 1
  |> Sum ()
taken.ToString () + "":"" + (join log)",
                "1:inner,outer"
            );
        }

        #endregion

        #region Async

        [Test]
        public void AwaitInsideTryWithCatch()
        {
            TestAsync(
                @"
fun fetch:Task<string> ->
    var result = """"
    try
        let value = await (delay 1)
        throw new System.InvalidOperationException (value.ToString ())
    catch e:System.InvalidOperationException
        let extra = await (delay 2)
        result = e.Message + "":"" + extra.ToString ()
    result

fetch ()",
                "2:4"
            );
        }

        [Test]
        public void AwaitInsideAFinallyBody()
        {
            TestAsync(
                @"
fun fetch:Task<string> ->
    var log = """"
    try
        let a = await (delay 1)
        log = a.ToString ()
    finally
        let b = await (delay 2)
        log = log + "":"" + b.ToString ()
    log

fetch ()",
                "2:4"
            );
        }

        [Test]
        public void TheFinallyRunsAndTheExceptionStillPropagates()
        {
            TestConfigured(
                Setup,
                @"
fun fetch:Task<string> (log:System.Collections.Generic.List<string>) ->
    try
        let a = await (delay 1)
        throw new System.InvalidOperationException ""boom""
    finally
        let b = await (delay 2)
        log.Add ""cleaned""
    """"

var log = new System.Collections.Generic.List<string> ()
let message = failure (fetch log)
message + "":"" + (join log)",
                "boom:cleaned"
            );
        }

        [Test]
        public void AwaitInsideUsing()
        {
            TestConfigured(
                c =>
                    {
                        Setup(c);
                        c.RegisterFunction("probe", new Func<List<string>, Probe>(log => new Probe(log)));
                    },
                @"
fun fetch:Task<string> (log:System.Collections.Generic.List<string>) ->
    var result = """"
    using p = probe log do
        let a = await (delay 3)
        result = a.ToString ()
    result

var log = new System.Collections.Generic.List<string> ()
let value = wait (fetch log)
value + "":"" + (join log)",
                "6:disposed"
            );
        }

        #endregion

        #region Helpers

        private static void Setup(LensCompiler compiler)
        {
            compiler.RegisterFunction("delay", new Func<int, Task<int>>(value => Task.Run(() =>
                        {
                            Task.Delay(5).Wait();
                            return value * 2;
                        }
                    )
                )
            );

            compiler.RegisterFunction("wait", new Func<Task<string>, string>(task => task.Result));
            compiler.RegisterFunction("join", new Func<List<string>, string>(log => string.Join(",", log)));
            compiler.RegisterFunction("failure", new Func<Task<string>, string>(task =>
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
            ));
        }

        private static void TestAsync(string src, object expected)
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            Setup(compiler);
            Assert.AreEqual(expected, ((Task<string>) compiler.Run(src)).Result);
        }

        #endregion
    }

    /// <summary>
    /// A resource that records having been disposed. Public and top-level, because the script is
    /// compiled into an assembly of its own.
    /// </summary>
    public class Probe : IDisposable
    {
        private readonly List<string> _log;

        public Probe(List<string> log)
        {
            _log = log;
        }

        public void Dispose()
        {
            _log.Add("disposed");
        }
    }
}
