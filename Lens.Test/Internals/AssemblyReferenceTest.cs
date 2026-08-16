using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using Lens.Analysis;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Internals
{
    /// <summary>
    /// What a 'declare reference' entry does: it is the only way a script that is read outside a
    /// host - by an editor, say - can name a type the host did not register.
    /// </summary>
    [TestFixture]
    internal class AssemblyReferenceTest
    {
        #region Helpers

        private static string TestFolder => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        private static Assembly Resolve(string spec, string baseDirectory = null)
        {
            Assert.IsTrue(
                AssemblyReferenceResolver.TryResolve(spec, baseDirectory, null, out var assembly, out var error),
                "The reference '{0}' did not resolve: {1}",
                spec,
                error
            );

            return assembly;
        }

        private static ScriptAnalysis Analyze(string source, LensCompilerOptions options = null, string baseDirectory = null)
        {
            return new ScriptAnalyzer(options).Analyze(source, baseDirectory);
        }

        #endregion

        #region Resolving

        [Test]
        public void APlatformAssemblyIsFoundByItsNameAlone()
        {
            // the shared framework lives wherever the runtime was installed, and a script that had
            // to spell that out would only run on the machine it was written on
            Assert.AreEqual(typeof(HttpClient).Assembly, Resolve("System.Net.Http"));
        }

        [Test]
        public void APlatformAssemblyIsFoundByItsFileNameToo()
        {
            Assert.AreEqual(typeof(HttpClient).Assembly, Resolve("System.Net.Http.dll"));
        }

        [Test]
        public void AnAssemblyBesideTheScriptIsFoundByARelativePath()
        {
            Assert.AreEqual(typeof(Assert).Assembly, Resolve("./nunit.framework.dll", TestFolder));
        }

        [Test]
        public void AnAssemblyIsFoundByAnAbsolutePath()
        {
            Assert.AreEqual(typeof(Assert).Assembly, Resolve(Path.Combine(TestFolder, "nunit.framework.dll")));
        }

        [Test]
        public void AnAlreadyReferencedAssemblyIsUsedRatherThanLoadedAgain()
        {
            // a second copy of the same file would give types that are not equal to the ones the
            // host hands over, and a script cannot be told why its own type is the wrong one
            var known = new[] {typeof(Assert).Assembly};

            Assert.IsTrue(AssemblyReferenceResolver.TryResolve("nunit.framework", null, known, out var assembly, out _));
            Assert.AreSame(typeof(Assert).Assembly, assembly);
        }

        [Test]
        public void AnAssemblyThatIsNotThereDoesNotResolve()
        {
            Assert.IsFalse(AssemblyReferenceResolver.TryResolve("./no/such/assembly.dll", TestFolder, null, out _, out var error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void AnEmptyReferenceDoesNotResolve()
        {
            Assert.IsFalse(AssemblyReferenceResolver.TryResolve("   ", null, null, out _, out _));
        }

        #endregion

        #region Analysis

        [Test]
        public void AReferencedTypeIsKnownToTheEditor()
        {
            using (var analysis = Analyze(@"
declare
    reference ""System.Net.Http""

use System.Net.Http
let http = new HttpClient ()"))
            {
                Assert.IsEmpty(analysis.Diagnostics);
            }
        }

        [Test]
        public void MembersOfAReferencedTypeAreOffered()
        {
            using (var analysis = Analyze(@"
declare
    reference ""System.Net.Http""

use System.Net.Http
let http = new HttpClient ()
http."))
            {
                var members = analysis.Complete(new LexemLocation {Line = 7, Offset = 6})
                                      .Select(x => x.Label)
                                      .ToArray();

                CollectionAssert.Contains(members, "BaseAddress");
            }
        }

        [Test]
        public void AReferenceIsResolvedRelativeToTheScript()
        {
            using (var analysis = Analyze(
                       @"
declare
    reference ""./nunit.framework.dll""

use NUnit.Framework
typeof TestCaseData",
                       baseDirectory: TestFolder
                   ))
            {
                Assert.IsEmpty(analysis.Diagnostics);
            }
        }

        [Test]
        public void AMissingReferenceIsAWarningAndNothingMore()
        {
            using (var analysis = Analyze(@"
declare
    reference ""./no/such/assembly.dll""

1 + 2"))
            {
                Assert.AreEqual(1, analysis.Diagnostics.Count);
                Assert.IsFalse(analysis.Diagnostics[0].IsError);
                StringAssert.Contains("LE3202", analysis.Diagnostics[0].Message);
            }
        }

        #endregion

        #region Safe mode

        [Test]
        public void ASandboxedScriptMayNotReferenceAssemblies()
        {
            // loading an assembly runs whatever its module initializer feels like running, which is
            // not something a sandboxed script gets to ask for
            var options = new LensCompilerOptions {SafeMode = SafeMode.Whitelist};

            using (var analysis = Analyze(@"
declare
    reference ""System.Net.Http""", options))
            {
                var problem = analysis.Diagnostics.FirstOrDefault(x => x.Message.StartsWith(CompilerMessages.SafeModeIllegalReference.Substring(0, 6)));

                Assert.IsNotNull(problem, "The reference was not rejected.");
                Assert.IsTrue(problem.IsError);
            }
        }

        #endregion
    }
}
