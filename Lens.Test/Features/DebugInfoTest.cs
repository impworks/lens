using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Lens.Compiler;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// A debuggable script has to be two things at once: still a script that runs and answers what
    /// it always did, and a body of code a debugger can be pointed back at the source of.
    ///
    /// Nothing here attaches a debugger - none can be attached from a test run. What is checked
    /// instead is the thing a debugger reads: the symbols really are loaded alongside the script,
    /// which is what makes a stack trace name the line of LENS that threw.
    /// </summary>
    [TestFixture]
    internal class DebugInfoTest : TestBase
    {
        #region Helpers

        private static LensCompilerOptions DebugOptions(Action<LensDebugSettings> setup = null)
        {
            var opts = new LensCompilerOptions();
            opts.DebugSettings.Enabled = true;
            setup?.Invoke(opts.DebugSettings);
            return opts;
        }

        /// <summary>
        /// Runs a script that is expected to throw, and reports the frame of the script itself.
        /// </summary>
        private static StackFrame FailingFrame(string src, LensCompilerOptions opts)
        {
            var compiler = new LensCompiler(opts);
            var script = compiler.Compile(src);

            var error = Assert.Throws<DivideByZeroException>(() => script());

            return new StackTrace(error, true)
                   .GetFrames()
                   .First(x => x.GetMethod()?.DeclaringType?.Assembly == script.Target?.GetType().Assembly);
        }

        #endregion

        #region Tests

        [Test]
        public void DebuggableScriptStillRuns()
        {
            var result = new LensCompiler(DebugOptions()).Run("let a = 1\nlet b = 2\na + b");
            Assert.AreEqual(3, result);
        }

        [Test]
        public void DebuggableScriptRunsMoreThanOnce()
        {
            var script = new LensCompiler(DebugOptions()).Compile("var x = 0\nx = x + 1\nx");

            Assert.AreEqual(1, script());
            Assert.AreEqual(1, script());
        }

        [Test]
        public void ConstantsAreNotUnrolledWhenDebugging()
        {
            // an unrolled name leaves no storage behind, so a debugger could show nothing of it -
            // the option the host set is deliberately overruled
            var ctx = new Context(DebugOptions());
            Assert.IsFalse(ctx.UnrollConstants);

            Assert.IsTrue(new Context(new LensCompilerOptions()).UnrollConstants);
        }

        [Test]
        public void StackTraceNamesTheLineThatThrew()
        {
            // the division is on the third line, and is the only thing that can throw
            var frame = FailingFrame("let a = 10\nvar b = 0\na / b", DebugOptions());

            Assert.AreEqual(3, frame.GetFileLineNumber());
        }

        [Test]
        public void StackTraceNamesTheConfiguredSourceFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "some_script.lns");
            var frame = FailingFrame("let a = 10\nvar b = 0\na / b", DebugOptions(x => x.SourceFile = path));

            Assert.AreEqual(path, frame.GetFileName());
        }

        [Test]
        public void StackTraceCarriesNoLinesWithoutDebugInfo()
        {
            var frame = FailingFrame("let a = 10\nvar b = 0\na / b", new LensCompilerOptions());

            Assert.AreEqual(0, frame.GetFileLineNumber());
        }

        [Test]
        public void FunctionBodiesAreMappedToTheirOwnLines()
        {
            var src = "fun divide:int (x:int y:int) ->\n"
                      + "    x / y\n"
                      + "\n"
                      + "divide 1 0";

            var frame = FailingFrame(src, DebugOptions());

            Assert.AreEqual("divide", frame.GetMethod().Name);
            Assert.AreEqual(2, frame.GetFileLineNumber());
        }

        [Test]
        public void LoopBodiesAreMappedToTheirOwnLines()
        {
            var src = "var i = 0\n"
                      + "var d = 1\n"
                      + "while i < 3 do\n"
                      + "    d = d - 1\n"
                      + "    i = i / d\n";

            // the third time round the loop the divisor is zero, and the line that divides is the
            // fifth - the loop has to be mapped per statement, not per loop
            var frame = FailingFrame(src, DebugOptions());

            Assert.AreEqual(5, frame.GetFileLineNumber());
        }

        [Test]
        public void LambdaBodiesAreMappedToTheirOwnLines()
        {
            var src = "let zero = 0\n"
                      + "let f = (x:int) -> x / zero\n"
                      + "f 1\n";

            // the body of a lambda is compiled into a method of its own, and the local it captures
            // lives in a closure class - neither of which may cost it its place in the source
            var frame = FailingFrame(src, DebugOptions());

            Assert.AreEqual(2, frame.GetFileLineNumber());
        }

        [Test]
        public void ScriptsWithClosuresAndLoopsStillRun()
        {
            // the shapes that generate the most code behind the author's back, compiled the
            // debuggable way: the answer has to be the one the script says
            var src = "var acc = 0\n"
                      + "let add = (x:int) -> acc = acc + x\n"
                      + "for i in 1..5 do\n"
                      + "    add i\n"
                      + "acc\n";

            Assert.AreEqual(10, new LensCompiler(DebugOptions()).Run(src));
        }

        [Test]
        public void IteratorsStillRunWhenDebugging()
        {
            var src = "fun upto:IEnumerable<int> (max:int) ->\n"
                      + "    for i in 1..max do\n"
                      + "        yield i\n"
                      + "\n"
                      + "upto 5\n"
                      + "  |> Sum ()\n";

            Assert.AreEqual(10, new LensCompiler(DebugOptions()).Run(src));
        }

        #endregion
    }
}
