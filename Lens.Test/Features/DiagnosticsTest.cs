using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class DiagnosticsTest : TestBase
    {
        [Test]
        public void SingleErrorIsStillASingleDiagnostic()
        {
            TestErrors(
                "nonexistent",
                CompilerMessages.IdentifierNotFound
            );
        }

        [Test]
        public void ThreeIndependentErrorsInThreeStatements()
        {
            TestErrors(
                @"
missing1
missing2
missing3",
                CompilerMessages.IdentifierNotFound,
                CompilerMessages.IdentifierNotFound,
                CompilerMessages.IdentifierNotFound
            );
        }

        [Test]
        public void ErrorsAreBoundToTheirOwnLocations()
        {
            var compiler = CreateCompiler(null);
            Assert.Throws<LensCompilerException>(() => compiler.Compile("missing1\nmissing2"));

            var lines = new System.Collections.Generic.List<int>();
            foreach (var diag in compiler.Diagnostics)
            {
                Assert.IsNotNull(diag.StartLocation);
                lines.Add(diag.StartLocation.Value.Line);
            }

            Assert.AreEqual(new[] {1, 2}, lines.ToArray());
        }

        [Test]
        public void ErrorsInSeveralFunctionsAreAllReported()
        {
            TestErrors(
                @"
fun a:int -> missing1
fun b:int -> missing2
1",
                CompilerMessages.IdentifierNotFound,
                CompilerMessages.IdentifierNotFound
            );
        }

        [Test]
        public void FirstErrorIsTheOneThrown()
        {
            var compiler = CreateCompiler(null);
            var ex = Assert.Throws<LensCompilerException>(() => compiler.Compile("missing1\nmissing2"));

            Assert.AreEqual(1, ex.StartLocation.Value.Line);
        }
    }
}
