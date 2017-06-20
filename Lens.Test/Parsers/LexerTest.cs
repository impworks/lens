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