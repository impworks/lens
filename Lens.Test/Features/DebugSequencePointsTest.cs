using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using Lens.Compiler;
using Lens.Lexer;
using Lens.Parser;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// The sequence points a compilation writes, read back out of the symbols it produced.
    ///
    /// This is the table a debugger consults to decide which line to highlight and where a step
    /// ends, so it is the only place the promise "stepping through a script behaves" can actually be
    /// checked. The first version of this feature passed every test that ran the code and still
    /// highlighted a whole loop as one statement, because nothing looked at the table.
    /// </summary>
    [TestFixture]
    internal class DebugSequencePointsTest : TestBase
    {
        #region Helpers

        /// <summary>
        /// One entry of the table: the line and columns it covers, or null for a hidden point.
        /// </summary>
        private class Point
        {
            public int StartLine;
            public int StartColumn;
            public int EndLine;
            public int EndColumn;
            public bool IsHidden;

            public override string ToString()
            {
                return IsHidden ? "<hidden>" : $"({StartLine},{StartColumn})-({EndLine},{EndColumn})";
            }
        }

        /// <summary>
        /// Compiles a script with symbols and reads the sequence points of its body back.
        /// </summary>
        private static List<Point> PointsOf(string src)
        {
            var opts = new LensCompilerOptions();
            opts.DebugSettings.Enabled = true;

            var ctx = new Context(opts);
            ctx.SetSource(src);
            ctx.Compile(new LensParser(new LensLexer(src).Lexems).Nodes);

            var reader = MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(ctx.DebugSymbols)).GetMetadataReader();

            // the script body is the method with the most points: everything else in the assembly is
            // a generated member, and those carry only what the statements they were built from did
            return reader.MethodDebugInformation
                         .Select(x => reader.GetMethodDebugInformation(x).GetSequencePoints().ToArray())
                         .OrderByDescending(x => x.Length)
                         .First()
                         .Select(x => new Point
                             {
                                 StartLine = x.StartLine,
                                 StartColumn = x.StartColumn,
                                 EndLine = x.EndLine,
                                 EndColumn = x.EndColumn,
                                 IsHidden = x.IsHidden
                             }
                         )
                         .ToList();
        }

        /// <summary>
        /// The points that name a line of the script, in the order they appear in the IL.
        /// </summary>
        private static List<Point> VisiblePointsOf(string src)
        {
            return PointsOf(src).Where(x => !x.IsHidden).ToList();
        }

        private static string Lines(params string[] lines)
        {
            return string.Join("\n", lines);
        }

        #endregion

        #region Tests

        [Test]
        public void EveryPointCoversOneLineOnly()
        {
            // the end recorded on a node runs past the statement - past the whole body, for anything
            // that has one - and a debugger given that highlights a loop entire and steps oddly
            var src = Lines(
                "var total = 0",
                "for i in 1..5 do",
                "    if i.even() then",
                "        total = total + i",
                "total"
            );

            foreach (var point in VisiblePointsOf(src))
                Assert.AreEqual(point.StartLine, point.EndLine, "A point spans more than its own line: {0}", point);
        }

        [Test]
        public void EveryPointCoversItsWholeLineAndNoMore()
        {
            var src = Lines(
                "var total = 0",
                "total = total + 1",
                "total"
            );

            var lines = src.Split('\n');
            foreach (var point in VisiblePointsOf(src))
            {
                var text = lines[point.StartLine - 1];
                Assert.AreEqual(text.TrimStart().Length + text.Length - text.TrimEnd().Length, text.Length, "unexpected trailing space in the fixture");
                Assert.AreEqual(text.Length + 1, point.EndColumn, "A point stops short of its line: {0}", point);
            }
        }

        [Test]
        public void StatementsAreMappedInOrder()
        {
            var src = Lines(
                "var a = 1",
                "var b = 2",
                "a + b"
            );

            Assert.AreEqual(
                new[] {"(1,1)-(1,10)", "(2,1)-(2,10)", "(3,1)-(3,6)"},
                VisiblePointsOf(src).Select(x => x.ToString()).ToArray()
            );
        }

        [Test]
        public void ALoopHeaderIsAPointOfItsOwn()
        {
            var src = Lines(
                "var total = 0",
                "for i in 1..5 do",
                "    total = total + i",
                "total"
            );

            var header = VisiblePointsOf(src).Where(x => x.StartLine == 2).ToArray();

            // twice: once where the loop is set up, and once at the top of the loop - which is what
            // returns the highlight to the 'for' on every iteration, as C# does
            Assert.AreEqual(2, header.Length, "The loop header should be entered and returned to.");
            Assert.AreEqual("(2,1)-(2,17)", header[0].ToString());
            Assert.AreEqual("(2,1)-(2,17)", header[1].ToString());
        }

        [Test]
        public void AWhileConditionIsAPointOfItsOwn()
        {
            var src = Lines(
                "var i = 0",
                "while i < 3 do",
                "    i = i + 1",
                "i"
            );

            // One point, not two: the statement and the condition it tests begin at the same
            // instruction, because the top of the loop is where the statement starts. The loop jumps
            // back to exactly that instruction, so this single point is both the 'while' the
            // highlight starts on and the one it returns to on every iteration.
            var condition = VisiblePointsOf(src).Where(x => x.StartLine == 2).ToArray();

            Assert.AreEqual(1, condition.Length);
            Assert.AreEqual("(2,1)-(2,15)", condition[0].ToString());
        }

        [Test]
        public void ALoopBodyIsMappedToItsOwnLines()
        {
            var src = Lines(
                "var total = 0",
                "for i in 1..5 do",
                "    total = total + i",
                "    total = total * 2",
                "total"
            );

            var body = VisiblePointsOf(src).Where(x => x.StartLine == 3 || x.StartLine == 4).ToArray();

            Assert.AreEqual(new[] {"(3,5)-(3,22)", "(4,5)-(4,22)"}, body.Select(x => x.ToString()).ToArray());
        }

        [Test]
        public void SynthesizedCodeIsHidden()
        {
            var src = Lines(
                "var total = 0",
                "for i in 1..5 do",
                "    total = total + i",
                "total"
            );

            // a 'for' becomes an index, a sign, an assignment and an increment, none of which its
            // author wrote - a debugger has to step over all of it
            Assert.IsTrue(PointsOf(src).Any(x => x.IsHidden), "The machinery a 'for' expands into should be hidden.");
        }

        [Test]
        public void NothingIsMarkedAtTheStartOfAMethod()
        {
            // a point at offset zero could never be replaced by the first statement's, and every
            // debugger already steps over the instructions before the first point
            var src = Lines(
                "var acc = 0",
                "let add = (x:int) -> acc = acc + x",
                "add 1",
                "acc"
            );

            foreach (var points in new[] {PointsOf(src)})
                Assert.IsFalse(points[0].IsHidden, "The first point of a method should name a line.");
        }

        #endregion
    }
}
