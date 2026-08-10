using System;
using System.Linq;
using Lens.Lexer;
using Lens.SyntaxTree;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Parsers
{
    [TestFixture]
    internal class LexerTest
    {
        [Test]
        public void StartNewlines()
        {
            var str = @"


let a = 1";

            Test(str,
                LexemType.Let,
                LexemType.Identifier,
                LexemType.Assign,
                LexemType.Int,
                LexemType.Eof
            );
        }

        [Test]
        public void EndNewlines()
        {
            var str = @"let a = 1



";

            Test(str,
                LexemType.Let,
                LexemType.Identifier,
                LexemType.Assign,
                LexemType.Int,
                LexemType.Eof
            );
        }


        [Test]
        public void BetweenNewlines()
        {
            var str = @"a = 1


b = 2";

            Test(str,
                LexemType.Identifier,
                LexemType.Assign,
                LexemType.Int,
                LexemType.NewLine,
                LexemType.Identifier,
                LexemType.Assign,
                LexemType.Int,
                LexemType.Eof
            );
        }

        [Test]
        public void StringErrorLocation()
        {
            TestError(
                @"let x = ""hello",
                LexerMessages.UnclosedString,
                ex =>
                {
                    Assert.AreEqual(new LexemLocation {Line = 1, Offset = 9}, ex.StartLocation);
                    Assert.AreEqual(new LexemLocation {Line = 1, Offset = 15}, ex.EndLocation);
                }
            );
        }

        [Test]
        public void StringEscapeTest()
        {
            void TestEscape(string str, string expectedString)
            {
                var lexer = new LensLexer(str);
                Assert.AreEqual(lexer.Lexems.Count, 2);
                Assert.AreEqual(lexer.Lexems[0].Type, LexemType.String);
                Assert.AreEqual(lexer.Lexems[0].Value, expectedString);
            }

            TestEscape(@"""\n""", "\n");
            TestEscape(@"""\t""", "\t");
            TestEscape(@"""\r""", "\r");
            TestEscape(@"""\\""", "\\");
            TestEscape("\"\\\\\"", "\\");
        }

        [Test]
        public void CharEscapeTest()
        {
            void TestEscape(string str, string expectedChar)
            {
                var lexer = new LensLexer(str);
                Assert.AreEqual(lexer.Lexems.Count, 2);
                Assert.AreEqual(lexer.Lexems[0].Type, LexemType.Char);
                Assert.AreEqual(lexer.Lexems[0].Value, expectedChar);
            }

            TestEscape(@"'\n'", "\n");
            TestEscape(@"'\t'", "\t");
            TestEscape(@"'\r'", "\r");
            TestEscape(@"'\\'", "\\");
            TestEscape("'\\\\'", "\\");
        }

        [Test]
        public void InvalidStringEscapeError()
        {
            TestError(@"""\x""", LexerMessages.UnknownEscape);
        }

        [Test]
        public void InvalidCharEscapeError()
        {
            TestError(@"'\x'", LexerMessages.UnknownEscape);
        }

        [Test]
        public void InvalidCharLiteralError1()
        {
            TestError(@"''", LexerMessages.IncorrectCharLiteral);
        }

        [Test]
        public void InvalidCharLiteralError2()
        {
            TestError(@"'hello'", LexerMessages.IncorrectCharLiteral);
        }

        [Test]
        public void InterpolatedStringIsASingleLexem()
        {
            Test(@"let a = $""x{1}y""",
                LexemType.Let,
                LexemType.Identifier,
                LexemType.Assign,
                LexemType.InterpolatedString,
                LexemType.Eof
            );
        }

        [Test]
        public void InterpolatedStringParts()
        {
            TestInterpolation(@"$""a{x}b{y:D2}""",
                "a", null,
                null, "x",
                "b", null,
                null, "y"
            );

            TestInterpolation(@"$""{{a}}""",
                "{a}", null
            );

            TestInterpolation(@"$""a""",
                "a", null
            );
        }

        [Test]
        public void InterpolatedStringFormatSpecifier()
        {
            var parts = new LensLexer(@"$""{x:D2}""").Lexems[0].InterpolationParts;
            Assert.AreEqual(1, parts.Length);
            Assert.AreEqual("x", parts[0].Expression);
            Assert.AreEqual("D2", parts[0].Format);
        }

        [Test]
        public void InterpolatedStringDoubleColonIsNotAFormatSpecifier()
        {
            var parts = new LensLexer(@"$""{int::MaxValue}""").Lexems[0].InterpolationParts;
            Assert.AreEqual(1, parts.Length);
            Assert.AreEqual("int::MaxValue", parts[0].Expression);
            Assert.IsNull(parts[0].Format);
        }

        [Test]
        public void InterpolatedStringNestedBraces()
        {
            var parts = new LensLexer(@"$""{new {1 => 2}}""").Lexems[0].InterpolationParts;
            Assert.AreEqual(1, parts.Length);
            Assert.AreEqual("new {1 => 2}", parts[0].Expression);
        }

        [Test]
        public void InterpolatedStringNestedQuotes()
        {
            var parts = new LensLexer(@"$""{""}\"""".Length}""").Lexems[0].InterpolationParts;
            Assert.AreEqual(1, parts.Length);
            Assert.AreEqual(@"""}\"""".Length", parts[0].Expression);
        }

        [Test]
        public void InterpolatedStringVerbatimEscaping()
        {
            TestInterpolation(@"$@""a\b""""c{1}""",
                @"a\b""c", null,
                null, "1"
            );
        }

        [Test]
        public void InterpolatedStringHoleLocation()
        {
            var parts = new LensLexer("$\"ab{x}\"\n  $\"c{y}\"").Lexems[0].InterpolationParts;
            Assert.AreEqual(new LexemLocation {Line = 1, Offset = 6}, parts[1].StartLocation);

            var parts2 = new LensLexer("$\"ab{x}\"\n  $\"c{y}\"").Lexems[2].InterpolationParts;
            Assert.AreEqual(new LexemLocation {Line = 2, Offset = 7}, parts2[1].StartLocation);
        }

        [Test]
        public void InterpolatedStringMultilineHoleLocation()
        {
            var parts = new LensLexer("$@\"a\nbc{x}\"").Lexems[0].InterpolationParts;
            Assert.AreEqual(new LexemLocation {Line = 2, Offset = 4}, parts[1].StartLocation);
        }

        [Test]
        public void UnclosedInterpolatedStringError()
        {
            TestError(@"$""abc", LexerMessages.UnclosedString);
        }

        [Test]
        public void UnclosedInterpolationHoleError()
        {
            TestError(@"$""abc{1 + 2", LexerMessages.UnclosedInterpolationHole);
        }

        [Test]
        public void UnclosedInterpolationFormatError()
        {
            TestError(@"$""abc{1:D2", LexerMessages.UnclosedInterpolationHole);
        }

        [Test]
        public void EmptyInterpolationHoleError()
        {
            TestError(@"$""abc{ }""", LexerMessages.EmptyInterpolationHole);
        }

        [Test]
        public void UnescapedInterpolationBraceError()
        {
            TestError(@"$""abc}""", LexerMessages.UnescapedInterpolationBrace);
        }

        /// <summary>
        /// Checks the segments of the first lexem, given as (literal, expression) pairs.
        /// </summary>
        private void TestInterpolation(string src, params string[] expected)
        {
            var parts = new LensLexer(src).Lexems[0].InterpolationParts;
            Assert.IsNotNull(parts, "The lexem is not an interpolated string!");

            var actual = parts.SelectMany(p => new[] {p.Literal, p.Expression}).ToArray();
            Assert.AreEqual(expected, actual);
        }

        private void Test(string str, params LexemType[] types)
        {
            var lexer = new LensLexer(str);
            Assert.AreEqual(types, lexer.Lexems.Select(l => l.Type).ToArray());
        }

        private void TestError(string src, string msg, Action<LensCompilerException> handler = null)
        {
            try
            {
                new LensLexer(src);
                Assert.Fail("No exception was thrown!");
            }
            catch (LensCompilerException ex)
            {
                var actualId = ex.Message.Substring(0, 6);
                var expectedId = msg.Substring(0, 6);
                Assert.AreEqual(expectedId, actualId);

                handler?.Invoke(ex);
            }
            catch
            {
                Assert.Fail("Incorrect exception type!");
            }
        }
    }
}