using Lens.SyntaxTree;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class StringInterpolationTest : TestBase
    {
        [Test]
        public void NoHoles()
        {
            Test(@"$""hello""", "hello");
        }

        [Test]
        public void NoHolesIsAPlainStringLiteral()
        {
            TestParser(@"$""hello""", Expr.Str("hello"));
        }

        [Test]
        public void SingleHole()
        {
            Test(@"$""{1}""", "1");
        }

        [Test]
        public void HoleWithExpression()
        {
            Test(@"$""the result is {1 + 2 * 3}!""", "the result is 7!");
        }

        [Test]
        public void ManyHoles()
        {
            Test(@"
let a = 1
let b = 2
$""{a} + {b} = {a + b}""",
                "1 + 2 = 3"
            );
        }

        [Test]
        public void AdjacentHoles()
        {
            Test(@"$""{1}{2}{3}""", "123");
        }

        [Test]
        public void EscapedBraces()
        {
            Test(@"$""{{{1}}}""", "{1}");
        }

        [Test]
        public void EscapedBracesWithoutHoles()
        {
            Test(@"$""{{literal}}""", "{literal}");
        }

        [Test]
        public void FormatSpecifier()
        {
            Test(@"$""[{10:D5}]""", "[00010]");
        }

        [Test]
        public void FormatSpecifierAndPlainHole()
        {
            // the format is applied with the current culture, exactly like a C# interpolated string
            Test(@"$""{255:X4} / {2}""", "00FF / 2");
        }

        [Test]
        public void NullHoleRendersAsEmptyString()
        {
            Test(@"$""[{null}]""", "[]");
        }

        [Test]
        public void EscapeSequences()
        {
            Test(@"$""a\t{1}\n""", "a\t1\n");
        }

        [Test]
        public void Verbatim()
        {
            Test(@"$@""a\b{1}""", @"a\b1");
        }

        [Test]
        public void VerbatimQuotes()
        {
            Test(@"$@""say """"{1}"""" now""", @"say ""1"" now");
        }

        [Test]
        public void VerbatimMultiline()
        {
            var src = "$@\"first {1}\nsecond {2}\"";
            Test(src, "first 1\nsecond 2");
        }

        [Test]
        public void PaddedHole()
        {
            Test(@"$""a{ 1 + 2 }b""", "a3b");
        }

        [Test]
        public void StaticMemberInHole()
        {
            Test(@"$""{int::MaxValue}""", "2147483647");
        }

        [Test]
        public void StringLiteralInHole()
        {
            Test(@"$""[{""x"".ToUpper()}]""", "[X]");
        }

        [Test]
        public void StringLiteralWithBracesInHole()
        {
            Test(@"$""[{""}{"".Length}]""", "[2]");
        }

        [Test]
        public void DictLiteralInHole()
        {
            Test(@"$""{(new {1 => 2; 3 => 4}).Count}""", "2");
        }

        [Test]
        public void LambdaInHole()
        {
            Test(@"$""{(x:int -> x * 2) 21}""", "42");
        }

        [Test]
        public void NestedInterpolatedString()
        {
            Test(@"$""a{$""b{1}c""}d""", "ab1cd");
        }

        [Test]
        public void InterpolationAsAnArgument()
        {
            Test(@"
let a = 21
string::Concat $""{a}"" ""!""",
                "21!"
            );
        }

        [Test]
        public void InterpolationInAChain()
        {
            Test(@"$""{1}{2}"".Length", 2);
        }

        [Test]
        public void UnclosedStringError()
        {
            TestError(@"let x = $""hello", LexerMessages.UnclosedString);
        }

        [Test]
        public void UnclosedHoleError()
        {
            TestError(@"let x = $""hello {1", LexerMessages.UnclosedInterpolationHole);
        }

        [Test]
        public void EmptyHoleError()
        {
            TestError(@"let x = $""hello {}""", LexerMessages.EmptyInterpolationHole);
        }

        [Test]
        public void UnescapedBraceError()
        {
            TestError(@"let x = $""hello}""", LexerMessages.UnescapedInterpolationBrace);
        }

        [Test]
        public void ErrorLocationInsideHole()
        {
            TestErrorLocation(
                @"let a = 1
let b = $""val: {unknown}""",
                CompilerMessages.IdentifierNotFound,
                new LexemLocation {Line = 2, Offset = 17}
            );
        }

        [Test]
        public void ErrorLocationInsideHoleOnSecondLine()
        {
            TestErrorLocation(
                "let b = $@\"first\nsecond {unknown}\"",
                CompilerMessages.IdentifierNotFound,
                new LexemLocation {Line = 2, Offset = 9}
            );
        }

        /// <summary>
        /// Checks that the compilation fails with the given message at the given location.
        /// </summary>
        private static void TestErrorLocation(string src, string msg, LexemLocation start)
        {
            var exception = Assert.Throws<LensCompilerException>(() => Compile(src));

            Assert.AreEqual(msg.Substring(0, 6), exception.Message.Substring(0, 6), exception.Message);
            Assert.AreEqual(start, exception.StartLocation);
        }
    }
}
