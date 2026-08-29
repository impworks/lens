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
        public void FunctionReturningObjectMustProduceAValue()
        {
            // 'object' is the base of everything, the pseudotype that stands for the absence of a
            // value included - which is exactly what must not be accepted here
            TestErrors(
                @"
fun add:object (arr:List<int>) ->
    arr.Add 1

add (new [[2]])",
                CompilerMessages.ReturnValueRequired
            );
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
        public void UsesOfANameWhoseDeclarationFailedAreNotReported()
        {
            // the mistake is in the value: the name is only missing because of it, and saying so
            // once per use buries the one diagnostic that points at the cause
            TestErrors(
                @"
var a = missing
println a
println a",
                CompilerMessages.IdentifierNotFound
            );
        }

        [Test]
        public void AssignmentToANameWhoseDeclarationFailedIsNotReported()
        {
            TestErrors(
                @"
var a = missing
a = 1",
                CompilerMessages.IdentifierNotFound
            );
        }

        [Test]
        public void AMethodThatExistsWithOtherArgumentsIsReportedAsAnOverloadMismatch()
        {
            // being told the method does not exist sends the reader looking for a typo in a name
            // that is spelled correctly
            TestErrors(
                @"
use System.Threading.Tasks
Task::Delay ""x""",
                CompilerMessages.TypeMethodArgumentsMismatch
            );
        }

        [Test]
        public void AMethodThatDoesNotExistAtAllIsStillReportedAsMissing()
        {
            TestErrors(
                @"
use System.Threading.Tasks
Task::Nonexistent 1",
                CompilerMessages.TypeStaticMethodNotFound
            );
        }

        [Test]
        public void AFunctionWithNoReturnTypeCannotReturnAValue()
        {
            TestErrors(
                @"
fun foo ->
    1

foo ()",
                CompilerMessages.ReturnValueFromVoidFunction
            );
        }

        [Test]
        public void AFunctionWithAReturnTypeMustProduceAValue()
        {
            TestErrors(
                @"
fun foo:int ->
    ()

foo ()",
                CompilerMessages.ReturnValueRequired
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
