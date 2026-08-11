using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// Closure analysis and closure emission are two separate passes. These cover the cases where
    /// the order the two used to be interleaved in was load-bearing.
    /// </summary>
    [TestFixture]
    internal class ClosureAnalysisTest : TestBase
    {
        [Test]
        public void OuterScopeGainsAClosureAfterTheInnerOneWasAnalysed()
        {
            // the inner loop captures 'b' and so needs a closure; the outer loop only turns out to
            // need one further down, after the inner scope has already been walked
            var src = @"
var fs = new List<Func<int>> ()
for a in Enumerable::Range 1 2 do
    for b in Enumerable::Range 1 2 do
        fs.Add (-> b)
    fs.Add (-> a)

fs.Select (f -> f ())
";
            Test(src, new[] {1, 2, 1, 1, 2, 2});
        }

        [Test]
        public void InnerLambdaCapturesFromBothLoopLevels()
        {
            var src = @"
var fs = new List<Func<int>> ()
for a in Enumerable::Range 1 2 do
    for b in Enumerable::Range 10 2 do
        fs.Add (-> a * 100 + b)

fs.Select (f -> f ())
";
            Test(src, new[] {110, 111, 210, 211});
        }

        [Test]
        public void LambdaNestedInsideLambdaCapturesAcrossMethodBoundary()
        {
            var src = @"
let outer = (x:int) ->
    let inner = (y:int) -> x + y
    inner 10

outer 5
";
            Test(src, 15);
        }

        [Test]
        public void CaptureInsideAGenericFunction()
        {
            var src = @"
fun pick<T>:T (a:T b:T) ->
    let f = -> a
    f ()

pick 7 9
";
            Test(src, 7);
        }
    }
}
