using System.Linq;
using Lens.LanguageServer.Core;
using NUnit.Framework;

namespace Lens.Test.Internals
{
    /// <summary>
    /// The editor-agnostic language services, and the invariants a protocol expects of them.
    ///
    /// A malformed range is not a cosmetic problem: an editor rejects the whole batch it arrives
    /// in, so one bad outline entry silently costs the file its outline.
    /// </summary>
    [TestFixture]
    internal class LanguageServiceTest
    {
        #region Helpers

        private const string Uri = "file:///c:/scripts/test.lns";

        private static LensLanguageService ServiceWith(string source)
        {
            var service = new LensLanguageService();
            service.Open(Uri, source);
            return service;
        }

        private static bool Precedes(TextPosition left, TextPosition right)
        {
            return left.Line < right.Line || (left.Line == right.Line && left.Character <= right.Character);
        }

        private static void AssertWellFormed(OutlineEntry entry)
        {
            Assert.IsTrue(
                Precedes(entry.Range.Start, entry.Range.End),
                "{0} '{1}': the range {2} ends before it starts",
                entry.Kind,
                entry.Name,
                entry.Range
            );

            Assert.IsTrue(
                Precedes(entry.Range.Start, entry.Selection.Start) && Precedes(entry.Selection.End, entry.Range.End),
                "{0} '{1}': the selection {2} is not inside the range {3}",
                entry.Kind,
                entry.Name,
                entry.Selection,
                entry.Range
            );

            foreach (var child in entry.Children)
                AssertWellFormed(child);
        }

        #endregion

        #region Outline ranges

        [Test]
        [TestCase("var a = 1")]
        [TestCase("fun f:int -> 1")]
        [TestCase("record P\n    X : int\n    Y : int")]
        [TestCase("type T\n    A\n    B of int")]
        [TestCase("fun f:int ->\n    var a = 1\n    a")]
        [TestCase("declare\n    let x : int\n    fun g:int (a:int)\n\nx")]
        [TestCase("record P\n    X : int\n\nfun f:int (p:P) -> p.X")]
        [TestCase("var a = 1\nvar = = =\nfun g:int -> 2")]
        public void OutlineRangesAreWellFormed(string source)
        {
            using (var service = ServiceWith(source))
            {
                var outline = service.Outline(Uri);
                Assert.IsNotEmpty(outline);

                foreach (var curr in outline)
                    AssertWellFormed(curr);
            }
        }

        [Test]
        public void ADeclarationClosedByADedentHasAnEnd()
        {
            // every construct with an indented body ends on a DEDENT, and a structural lexem used to
            // carry no end position at all - which left the whole declaration ending nowhere
            using (var service = ServiceWith("record P\n    X : int\n    Y : int"))
            {
                var record = service.Outline(Uri).Single();

                Assert.AreEqual(0, record.Range.Start.Line);
                Assert.GreaterOrEqual(record.Range.End.Line, 2);
            }
        }

        #endregion

        #region Documents

        [Test]
        public void ChangingADocumentReanalysesIt()
        {
            using (var service = ServiceWith("undefinedName"))
            {
                Assert.IsNotEmpty(service.Diagnose(Uri));

                service.Change(Uri, "var a = 1", 2);
                Assert.IsEmpty(service.Diagnose(Uri));
            }
        }

        [Test]
        public void ClosingADocumentForgetsIt()
        {
            using (var service = ServiceWith("var a = 1"))
            {
                service.Close(Uri);

                Assert.IsNull(service.Find(Uri));
                Assert.IsEmpty(service.Diagnose(Uri));
            }
        }

        [Test]
        public void AMissingReferenceIsAWarningAndNotAnError()
        {
            using (var service = ServiceWith("declare\n    reference \"./no/such/assembly.dll\"\n\n1"))
            {
                var problems = service.Diagnose(Uri);

                Assert.AreEqual(1, problems.Count);
                Assert.AreEqual(ProblemSeverity.Warning, problems[0].Severity);
            }
        }

        #endregion

        #region Rename

        [Test]
        public void RenamingALocalEditsEveryMention()
        {
            using (var service = ServiceWith("var count = 1\ncount + count"))
            {
                var outcome = service.Rename(Uri, new TextPosition(0, 4), "total");

                Assert.IsTrue(outcome.IsAllowed);
                Assert.AreEqual(3, outcome.Edits.Count);
                Assert.IsTrue(outcome.Edits.All(x => x.Text == "total"));
            }
        }

        [Test]
        public void RenamingIntoAnExistingNameIsRefused()
        {
            using (var service = ServiceWith("var count = 1\nvar total = 2\ncount + total"))
            {
                var outcome = service.Rename(Uri, new TextPosition(0, 4), "total");

                Assert.IsFalse(outcome.IsAllowed);
                Assert.IsNotEmpty(outcome.Refusal);
            }
        }

        [Test]
        public void RenamingIntoAKeywordIsRefused()
        {
            using (var service = ServiceWith("var count = 1\ncount"))
            {
                var outcome = service.Rename(Uri, new TextPosition(0, 4), "match");

                Assert.IsFalse(outcome.IsAllowed);
            }
        }

        [Test]
        public void RenamingSomethingTheScriptDoesNotOwnIsRefused()
        {
            using (var service = ServiceWith("var text = \"hi\"\nvar size = text.Length"))
            {
                var outcome = service.Rename(Uri, new TextPosition(1, 18), "Size");

                Assert.IsFalse(outcome.IsAllowed);
                Assert.IsNotEmpty(outcome.Refusal);
            }
        }

        #endregion

        #region Colouring

        [Test]
        public void NoColouredRunCrossesALineBreak()
        {
            using (var service = ServiceWith("var a = @\"one\ntwo\"\nvar b = a"))
            {
                var runs = service.Colour(Uri);

                Assert.IsNotEmpty(runs);
                Assert.IsTrue(runs.All(x => x.Length > 0));
            }
        }

        #endregion
    }
}
