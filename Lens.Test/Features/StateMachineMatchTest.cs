using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// Suspending inside a match.
    ///
    /// A match opens no protected region: the node already expands into a flat run of labels and
    /// jumps, so a dispatch can land in the middle of a case body. Arriving there skips the rule
    /// checks and the declarations of the names the pattern bound - which is right, because inside
    /// a machine those names are fields and still hold what the matching check put there.
    /// </summary>
    [TestFixture]
    internal class StateMachineMatchTest : TestBase
    {
        #region Iterators

        [Test]
        public void YieldInsideACaseBody()
        {
            Test(
                @"
fun describe:IEnumerable<string> (n:int) ->
    match n with
        case 1 then
            yield ""one""
            yield ""uno""
        case 2 then
            yield ""two""
        case _ then
            yield ""many""

describe 1
  |> Aggregate (a:string b:string) -> a + "","" + b",
                "one,uno"
            );
        }

        [Test]
        public void PatternBoundNamesSurviveTheSuspension()
        {
            Test(
                @"
fun describe:IEnumerable<string> (n:int) ->
    match n with
        case x:int then
            yield ""first:"" + x.ToString ()
            yield ""second:"" + x.ToString ()

describe 7
  |> Aggregate (a:string b:string) -> a + "","" + b",
                "first:7,second:7"
            );
        }

        [Test]
        public void MatchInsideALoop()
        {
            Test(
                @"
fun describe:IEnumerable<string> (max:int) ->
    for i in 1..max do
        match i % 2 with
            case 0 then
                yield ""even""
            case _ then
                yield ""odd:"" + i.ToString ()

describe 4
  |> Aggregate (a:string b:string) -> a + "","" + b",
                "odd:1,even,odd:3"
            );
        }

        [Test]
        public void MatchOnADeconstructedRecord()
        {
            Test(
                @"
record Point
    X : int
    Y : int

fun walk:IEnumerable<string> (p:Point) ->
    match p with
        case Point(X = 0; Y = y) then
            yield ""on-axis""
            yield y.ToString ()
        case Point(X = x; Y = y) then
            yield x.ToString ()
            yield y.ToString ()

walk (new Point 0 5)
  |> Aggregate (a:string b:string) -> a + "","" + b",
                "on-axis,5"
            );
        }

        [Test]
        public void MatchInsideATry()
        {
            Test(
                @"
fun describe:IEnumerable<string> (n:int) ->
    var log = """"
    try
        match n with
            case 1 then
                yield ""one""
                throw new System.InvalidOperationException ""boom""
            case _ then
                yield ""other""
    catch e:System.InvalidOperationException
        yield ""caught:"" + e.Message
    finally
        log = ""done""
    yield log

describe 1
  |> Aggregate (a:string b:string) -> a + "","" + b",
                "one,caught:boom,done"
            );
        }

        [Test]
        public void MatchWithAGuard()
        {
            Test(
                @"
fun describe:IEnumerable<string> (n:int) ->
    match n with
        case x:int when x > 10 then
            yield ""big""
            yield x.ToString ()
        case x:int then
            yield ""small""

describe 42
  |> Aggregate (a:string b:string) -> a + "","" + b",
                "big,42"
            );
        }

        #endregion

        #region Async

        [Test]
        public void AwaitInsideACaseBody()
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            compiler.RegisterFunction("delay", new Func<int, Task<int>>(value => Task.Run(() =>
                        {
                            Task.Delay(5).Wait();
                            return value * 2;
                        }
                    )
                )
            );

            var result = compiler.Run(@"
fun fetch:Task<string> (n:int) ->
    var result = """"
    match n with
        case 1 then
            let a = await (delay 1)
            let b = await (delay 2)
            result = ""one:"" + (a + b).ToString ()
        case _ then
            let c = await (delay 10)
            result = ""other:"" + c.ToString ()
    result

fetch 1");

            Assert.AreEqual("one:6", ((Task<string>) result).Result);
        }

        #endregion

        #region Rejections

        [Test]
        public void ASuspendedMatchCannotProduceAValue()
        {
            TestError(
                @"
fun broken:IEnumerable<int> (t:System.Threading.Tasks.Task<int> n:int) ->
    let x = match n with
                case 1 then await t
                case _ then 0
    yield x",
                "LE3179"
            );
        }

        [Test]
        public void ALambdaInsideASuspendedMatchIsRejected()
        {
            TestError(
                @"
fun broken:IEnumerable<int> (n:int) ->
    match n with
        case 1 then
            let f = -> 1
            yield f ()
        case _ then
            yield 0",
                "LE3180"
            );
        }

        #endregion
    }
}
