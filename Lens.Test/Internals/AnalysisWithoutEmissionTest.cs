using System.Linq;
using Lens.Compiler;
using NUnit.Framework;

namespace Lens.Test.Internals
{
    /// <summary>
    /// Analysis and emission are separate. A script can be read, bound and diagnosed without an
    /// assembly ever being defined - which is what a language server needs, since it re-analyses on
    /// every keystroke and cannot leak a dynamic module each time.
    /// </summary>
    [TestFixture]
    internal class AnalysisWithoutEmissionTest : TestBase
    {
        private static Context Analyze(string src)
        {
            var ctx = new Context(new LensCompilerOptions());
            ctx.Analyze(Parse(src).ToList());
            return ctx;
        }

        [Test]
        public void ConstructingAContextBuildsNoAssembly()
        {
            var ctx = new Context(new LensCompilerOptions());
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAnExpressionBuildsNoAssembly()
        {
            var ctx = Analyze("1 + 2 * 3");

            Assert.IsFalse(ctx.HasEmitTarget, "analysis must not define an assembly");
            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
        }

        [Test]
        public void AnalysingLocalsAndLambdasBuildsNoAssembly()
        {
            var ctx = Analyze(@"
var a = 1
let f = (x:int) -> x + a
f 2");

            Assert.IsFalse(ctx.HasEmitTarget);
            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
        }

        [Test]
        public void AnalysingARecordBuildsNoAssembly()
        {
            var ctx = Analyze(@"
record Point
    X : int
    Y : int

new Point 1 2");

            Assert.IsFalse(ctx.HasEmitTarget, "mentioning a record must not force its TypeBuilder");
            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
        }

        [Test]
        public void AnalysingAGenericFunctionBuildsNoAssembly()
        {
            var ctx = Analyze(@"
fun id<T>:T (x:T) -> x
id 5");

            Assert.IsFalse(ctx.HasEmitTarget);
            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
        }

        [Test]
        public void AnalysingAGenericOverADeclaredTypeBuildsNoAssembly()
        {
            var ctx = Analyze(@"
record P
    X : int

var l = new List<P> ()
l.Add (new P 1)
l");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty, "a declared type inside a generic must resolve");
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAnArrayOfATypeParameterBuildsNoAssembly()
        {
            var ctx = Analyze("fun first<T>:T (xs:T[]) -> xs[0]\nfirst (new[1; 2])");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAGenericRecordBuildsNoAssembly()
        {
            var ctx = Analyze("record B<T>\n    V : T\n\n1");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAGenericRecordWithACompositeFieldBuildsNoAssembly()
        {
            var ctx = Analyze("record B<T>\n    V : List<T>\n\n1");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAGenericAlgebraicTypeBuildsNoAssembly()
        {
            var ctx = Analyze("type Opt<T>\n    Some of T\n    None\n\n1");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysisReportsErrorsWithoutThrowing()
        {
            var ctx = Analyze("missing1\nmissing2");

            Assert.IsTrue(ctx.Diagnostics.HasErrors);
            Assert.AreEqual(2, ctx.Diagnostics.Count);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysisCanBeRepeatedOnTheSameTree()
        {
            var nodes = Parse("var x = 1\nlet y = x + 1\ny").ToList();

            var first = new Context(new LensCompilerOptions());
            first.Analyze(nodes);

            var second = new Context(new LensCompilerOptions());
            second.Analyze(nodes);

            Assert.IsTrue(first.Diagnostics.IsEmpty);
            Assert.IsTrue(second.Diagnostics.IsEmpty);
            Assert.IsFalse(first.HasEmitTarget);
            Assert.IsFalse(second.HasEmitTarget);
        }

        [Test]
        public void AnalysingAMatchBuildsNoAssembly()
        {
            // 'match' expands into jumps, and a jump label used to be reserved on the ILGenerator
            // while the tree was being bound - so a script containing one could not be analysed at
            // all, which is most of what a LENS script does
            var ctx = Analyze(@"
type Shape
    Empty
    Dot of int

fun describe:string (s:Shape) ->
    match s with
        case Empty then ""nothing""
        case Dot of n then ""a dot""

describe Empty");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAMatchProducingARecordBuildsNoAssembly()
        {
            // the fallback branch of a match that returns a value asks for the default of its type,
            // and materializing a declared type is what forces its builder into being
            var ctx = Analyze(@"
record Point
    X : int

fun pick:Point (flag:bool) ->
    match flag with
        case true then new Point 1
        case _ then new Point 2

pick true");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAMatchOverAGenericTypeInsideAGenericFunctionBuildsNoAssembly()
        {
            // the type a pattern names is the label instantiated with the arguments of the value
            // being matched - and both halves of that used to be built out of builders: the
            // declaration has none before emission, and neither has the T it is instantiated with
            var ctx = Analyze(@"
type Opt<T>
    None
    Some of T

fun map<T>:T (item:Opt<T> def:T) ->
    match item with
        case Some of x then x
        case None then def

map (Some 1) 0");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAFunctionThatReturnsNoValueReportsIt()
        {
            // the check used to live in the emission half only, so an editor - which never emits -
            // saw nothing wrong with a function whose body produces no value at all
            var ctx = Analyze(@"
fun add<T>:T (item:T arr:List<T>) ->
    arr.Add item

add 1 (new [[2]])");

            Assert.IsTrue(ctx.Diagnostics.HasErrors);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void CompilingStillBuildsAnAssembly()
        {
            var ctx = new Context(new LensCompilerOptions());
            ctx.Compile(Parse("1 + 2").ToList());

            Assert.IsTrue(ctx.HasEmitTarget);
        }
    }
}
