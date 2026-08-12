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
        public void CompilingStillBuildsAnAssembly()
        {
            var ctx = new Context(new LensCompilerOptions());
            ctx.Compile(Parse("1 + 2").ToList());

            Assert.IsTrue(ctx.HasEmitTarget);
        }
    }
}
