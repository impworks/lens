using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class IteratorsTest : TestBase
    {
        #region Yielding

        [Test]
        public void SimpleSequence()
        {
            Test(
                @"
fun three:IEnumerable<int> ->
    yield 1
    yield 2
    yield 3

three ()
  |> Sum ()",
                6
            );
        }

        [Test]
        public void SequenceOverRange()
        {
            Test(
                @"
fun upto:IEnumerable<int> (max:int) ->
    for i in 1..max do
        yield i

upto 5
  |> Sum ()",
                10
            );
        }

        [Test]
        public void SequenceOverSequence()
        {
            Test(
                @"
fun doubled:IEnumerable<int> (items:IEnumerable<int>) ->
    for x in items do
        yield x * 2

doubled (new [1; 2; 3])
  |> Sum ()",
                12
            );
        }

        [Test]
        public void SequenceWithCondition()
        {
            Test(
                @"
fun evens:IEnumerable<int> (max:int) ->
    for i in 0..max do
        if i % 2 == 0 then
            yield i

evens 10
  |> Sum ()",
                20
            );
        }

        [Test]
        public void SequenceOverWhileLoop()
        {
            Test(
                @"
fun powers:IEnumerable<int> (limit:int) ->
    var value = 1
    while value < limit do
        yield value
        value = value * 2

powers 100
  |> Sum ()",
                127
            );
        }

        [Test]
        public void TheEnumerableIsLazyAndInfinite()
        {
            Test(
                @"
fun naturals:IEnumerable<int> ->
    var i = 1
    while true do
        yield i
        i = i + 1

naturals ()
  |> Take 5
  |> Sum ()",
                15
            );
        }

        [Test]
        public void SequenceIsRestartable()
        {
            Test(
                @"
fun three:IEnumerable<int> ->
    yield 1
    yield 2
    yield 3

let items = three ()
let first = items
  |> Sum ()
let second = items
  |> Sum ()
first + second",
                12
            );
        }

        [Test]
        public void ArgumentsSurviveIntoTheMachine()
        {
            Test(
                @"
fun between:IEnumerable<string> (lo:int hi:int sep:string) ->
    for i in lo..hi do
        yield sep + i.ToString ()

between 1 4 ""-""
  |> Aggregate (a:string b:string) -> a + b",
                "-1-2-3"
            );
        }

        #endregion

        #region Yield from

        [Test]
        public void YieldFromAnotherIterator()
        {
            Test(
                @"
fun inner:IEnumerable<int> ->
    yield 1
    yield 2

fun outer:IEnumerable<int> ->
    yield 0
    yield from inner ()
    yield 3

outer ()
  |> Aggregate (a:int b:int) -> a * 10 + b",
                123
            );
        }

        [Test]
        public void YieldFromArray()
        {
            Test(
                @"
fun flat:IEnumerable<int> ->
    yield from new [1; 2]
    yield from new [3; 4]

flat ()
  |> Sum ()",
                10
            );
        }

        [Test]
        public void YieldFromIsLazy()
        {
            Test(
                @"
fun naturals:IEnumerable<int> ->
    var i = 1
    while true do
        yield i
        i = i + 1

fun prefixed:IEnumerable<int> ->
    yield 0
    yield from naturals ()

prefixed ()
  |> Take 4
  |> Sum ()",
                6
            );
        }

        #endregion

        #region Closures

        [Test]
        public void LambdaCapturesALocalLiveAcrossAYield()
        {
            // the local is hoisted into the machine because it is live across the yield, and into a
            // closure because the lambda captures it - and there is only one of it either way
            Test(
                @"
fun counted:IEnumerable<int> ->
    var n = 1
    let bump = -> n = n * 3
    yield n
    bump ()
    yield n
    n = n + 1
    yield n
    bump ()
    yield n

counted ()
  |> Aggregate (a:int b:int) -> a * 100 + b",
                1030412
            );
        }

        [Test]
        public void LambdaSeesTheMachineFieldItCaptured()
        {
            // both lambdas close over the same hoisted 'n'. The first one is read before the
            // machine resumes and assigns 42, so it answers 0; the second one answers 42, and so
            // would the first one if it were read afterwards.
            Test(
                @"
fun readers:IEnumerable<System.Func<int>> ->
    var n = 0
    yield (-> n)
    n = 42
    yield (-> n)

var sum = 0
var last = 0
for f in readers () do
    sum = sum + f ()
    last = f ()
sum * 100 + last",
                4242
            );
        }

        #endregion

        #region Host interop

        [Test]
        public void ConsumedByAHostMethod()
        {
            TestConfigured(
                c => c.RegisterFunction("join", (IEnumerable<string> items) => string.Join(",", items.ToArray())),
                @"
fun words:IEnumerable<string> ->
    yield ""a""
    yield ""b""
    yield ""c""

join (words ())",
                "a,b,c"
            );
        }

        [Test]
        public void HostSeesTheItemsLazily()
        {
            TestConfigured(
                c => c.RegisterFunction("firstTwo", (IEnumerable<int> items) => items.Take(2).Sum()),
                @"
fun naturals:IEnumerable<int> ->
    var i = 1
    while true do
        yield i
        i = i + 1

firstTwo (naturals ())",
                3
            );
        }

        /// <summary>
        /// The non-generic half of the protocol is implemented under names of its own, because
        /// IEnumerable and IEnumerable&lt;T&gt; both want a member called GetEnumerator. Nothing in
        /// LENS reaches it - only a host that took the untyped interface does.
        /// </summary>
        [Test]
        public void ConsumedThroughTheNonGenericInterface()
        {
            TestConfigured(
                c => c.RegisterFunction("total", (IEnumerable items) =>
                    {
                        var sum = 0;
                        foreach (var curr in items)
                            sum += (int) curr;
                        return sum;
                    }
                ),
                @"
fun three:IEnumerable<int> ->
    yield 1
    yield 2
    yield 3

total (three ())",
                6
            );
        }

        [Test]
        public void ResetIsNotSupported()
        {
            TestConfigured(
                c => c.RegisterFunction("resets", (IEnumerable<int> items) =>
                    {
                        var iter = items.GetEnumerator();
                        iter.MoveNext();
                        try
                        {
                            ((IEnumerator) iter).Reset();
                            return "no error";
                        }
                        catch (NotSupportedException)
                        {
                            return "not supported";
                        }
                    }
                ),
                @"
fun three:IEnumerable<int> ->
    yield 1
    yield 2

resets (three ())",
                "not supported"
            );
        }

        #endregion

        #region Generics

        [Test]
        public void GenericIterator()
        {
            Test(
                @"
fun twice<T>:IEnumerable<T> (item:T) ->
    yield item
    yield item

twice ""a""
  |> Aggregate (a:string b:string) -> a + b",
                "aa"
            );
        }

        [Test]
        public void GenericIteratorOverASequence()
        {
            Test(
                @"
fun pairs<T>:IEnumerable<T> (items:IEnumerable<T>) ->
    for x in items do
        yield x
        yield x

pairs (new [1; 2])
  |> Sum ()",
                6
            );
        }

        [Test]
        public void GenericIteratorWithTwoParameters()
        {
            Test(
                @"
fun zip<A, B>:IEnumerable<string> (a:A b:B) ->
    yield a.ToString ()
    yield b.ToString ()

zip 1 true
  |> Aggregate (x:string y:string) -> x + "" "" + y",
                "1 True"
            );
        }

        [Test]
        public void GenericIteratorOverADeclaredType()
        {
            Test(
                @"
record Box
    Value : int

fun unwrap<T>:IEnumerable<T> (item:T) ->
    yield item

var total = 0
for b in unwrap (new Box 42) do
    total = total + b.Value
total",
                42
            );
        }

        [Test]
        public void GenericIteratorWithAConstraint()
        {
            Test(
                @"
fun described<T = System.IComparable>:IEnumerable<string> (item:T) ->
    yield item.ToString ()

described 7
  |> Aggregate (a:string b:string) -> a + b",
                "7"
            );
        }

        #endregion

        #region Rejections

        [Test]
        public void YieldOutsideAFunctionIsRejected()
        {
            TestError("yield 1", "LE3166");
        }

        [Test]
        public void UndeclaredReturnTypeIsRejected()
        {
            TestError(
                @"
fun broken ->
    yield 1",
                "LE3167"
            );
        }

        [Test]
        public void NonSequenceReturnTypeIsRejected()
        {
            TestError(
                @"
fun broken:int ->
    yield 1",
                "LE3168"
            );
        }

        [Test]
        public void PureIteratorIsRejected()
        {
            TestError(
                @"
pure fun broken:IEnumerable<int> ->
    yield 1",
                "LE3169"
            );
        }

        [Test]
        public void YieldInsideTryIsRejected()
        {
            TestError(
                @"
fun broken:IEnumerable<int> ->
    try
        yield 1
    catch
        ()",
                "LE3170"
            );
        }

        [Test]
        public void YieldInsideMatchIsRejected()
        {
            TestError(
                @"
fun broken:IEnumerable<int> (x:int) ->
    match x with
        case 1 then yield 1
        case _ then yield 2",
                "LE3170"
            );
        }

        [Test]
        public void YieldInsideLambdaIsRejected()
        {
            TestError(
                @"
fun broken:IEnumerable<int> ->
    let f = ->
        yield 1
    yield 2",
                "LE3171"
            );
        }

        #endregion
    }
}
