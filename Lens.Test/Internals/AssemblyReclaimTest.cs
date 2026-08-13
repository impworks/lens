using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using NUnit.Framework;

namespace Lens.Test.Internals
{
    /// <summary>
    /// The dynamic assembly a compilation builds must be reclaimable once nothing refers to it.
    ///
    /// Every compilation used to leak one for the lifetime of the process, which is survivable for
    /// a build step and fatal for an editor that recompiles on every keystroke.
    /// </summary>
    [TestFixture]
    internal class AssemblyReclaimTest : TestBase
    {
        /// <summary>
        /// Builds and drops a number of assemblies, returning a weak reference to each so the test
        /// can ask the collector whether they survived. Deliberately not inlined: the locals of this
        /// method must go out of scope before the collection happens.
        /// </summary>
        private static List<WeakReference> BuildAndDrop(int count, Action<Context> work)
        {
            var result = new List<WeakReference>();

            for (var idx = 0; idx < count; idx++)
            {
                var ctx = new Context(new LensCompilerOptions());
                work(ctx);
                result.Add(new WeakReference(ctx.MainAssembly));
            }

            return result;
        }

        private static void Collect()
        {
            for (var idx = 0; idx < 3; idx++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
            }
        }

        [Test]
        public void AnAssemblyBuiltForAnalysisIsReclaimed()
        {
            var refs = BuildAndDrop(10, ctx => { });

            Collect();

            Assert.IsTrue(
                refs.Any(x => !x.IsAlive),
                "no dynamic assembly was reclaimed - the emit target is not collectible on this runtime"
            );
        }

        [Test]
        public void AnAssemblyBuiltForACompilationIsReclaimed()
        {
            var refs = BuildAndDrop(10, ctx => ctx.Compile(Parse("var a = 1\nlet f = (x:int) -> x + a\nf 2").ToList()));

            Collect();

            Assert.IsTrue(
                refs.Any(x => !x.IsAlive),
                "a compiled script's assembly is never reclaimed"
            );
        }

        [Test]
        public void ARunningScriptKeepsItsAssemblyAlive()
        {
            // the other half of the contract: collectible must not mean collected too early
            var compiler = new LensCompiler(new LensCompilerOptions());
            var run = compiler.Compile("var a = 40\nlet f = (x:int) -> x + a\nf 2");

            Collect();

            Assert.AreEqual(42, run());
        }
    }
}
