using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// Checks the lowering pass on its own, before any state machine is involved.
    ///
    /// Every case is compiled twice: once as the parser produced it, and once with its control flow
    /// flattened into labels and jumps. A script that contains no resume point must not be able to
    /// tell the difference - which is the property the state machine transform then relies on.
    /// </summary>
    [TestFixture]
    internal class LoweringTest : TestBase
    {
        [Test]
        public void IfWithoutElse()
        {
            TestLowered(
                @"
var x = 0
if 2 > 1 then
    x = 10
x",
                10
            );
        }

        [Test]
        public void IfWithElse()
        {
            TestLowered(
                @"
var x = 0
if 1 > 2 then
    x = 10
else
    x = 20
x",
                20
            );
        }

        [Test]
        public void NestedIfs()
        {
            TestLowered(
                @"
fun classify:string (n:int) ->
    var result = ""?""
    if n < 0 then
        result = ""negative""
    else
        if n == 0 then
            result = ""zero""
        else
            result = ""positive""
    result

classify (-5) + "" "" + (classify 0) + "" "" + (classify 7)",
                "negative zero positive"
            );
        }

        [Test]
        public void WhileLoop()
        {
            TestLowered(
                @"
var i = 0
var sum = 0
while i < 5 do
    sum = sum + i
    i = i + 1
sum",
                10
            );
        }

        [Test]
        public void NestedWhileLoops()
        {
            TestLowered(
                @"
var i = 0
var total = 0
while i < 3 do
    var j = 0
    while j < 3 do
        total = total + 1
        j = j + 1
    i = i + 1
total",
                9
            );
        }

        [Test]
        public void ForOverRange()
        {
            TestLowered(
                @"
var sum = 0
for i in 1..5 do
    sum = sum + i
sum",
                10
            );
        }

        [Test]
        public void ForOverDescendingRange()
        {
            TestLowered(
                @"
var acc = """"
for i in 3..0 do
    acc = acc + i.ToString ()
acc",
                "321"
            );
        }

        [Test]
        public void ForOverArray()
        {
            TestLowered(
                @"
var sum = 0
for x in new [1; 2; 3; 4] do
    sum = sum + x
sum",
                10
            );
        }

        [Test]
        public void ForOverList()
        {
            TestLowered(
                @"
var acc = """"
for x in new [[""a""; ""b""; ""c""]] do
    acc = acc + x
acc",
                "abc"
            );
        }

        [Test]
        public void ForWithInnerIf()
        {
            TestLowered(
                @"
var sum = 0
for i in 1..10 do
    if i % 2 == 0 then
        sum = sum + i
sum",
                20
            );
        }

        [Test]
        public void LoopWithClosure()
        {
            TestLowered(
                @"
var fxs = new System.Collections.Generic.List<System.Func<int>> ()
for i in 1..4 do
    let captured = i
    fxs.Add (-> captured)

var sum = 0
for f in fxs do
    sum = sum + f ()
sum",
                6
            );
        }

        [Test]
        public void TryInsideLoop()
        {
            TestLowered(
                @"
var caught = 0
for i in 1..4 do
    try
        if i == 2 then
            throw new System.Exception ""boom""
    catch
        caught = caught + 1
caught",
                1
            );
        }

        [Test]
        public void MatchInsideLoop()
        {
            TestLowered(
                @"
var acc = """"
for i in 1..4 do
    let part = match i with
               case 1 then ""one""
               case 2 then ""two""
               case _ then ""many""
    acc = acc + part
acc",
                "onetwomany"
            );
        }

        [Test]
        public void ValuePositionIsPreserved()
        {
            TestLowered(
                @"
fun pick:int (flag:bool) ->
    if flag then
        1
    else
        2

(pick true) + (pick false)",
                3
            );
        }

        [Test]
        public void LoopAsFunctionResult()
        {
            TestLowered(
                @"
fun countdown:string ->
    var acc = """"
    for i in 5..0 do
        acc = acc + i.ToString ()
    acc

countdown ()",
                "54321"
            );
        }

        /// <summary>
        /// Compiles a script both ways and requires the same answer from each.
        /// </summary>
        private static void TestLowered(string src, object expected)
        {
            Assert.AreEqual(expected, Compile(src, Options(false)), "The script itself is wrong!");
            Assert.AreEqual(expected, Compile(src, Options(true)), "Lowering changed the script's meaning!");
        }

        private static LensCompilerOptions Options(bool lower)
        {
            return new LensCompilerOptions
            {
                UnrollConstants = true,
                LowerAllFunctions = lower
            };
        }
    }
}
