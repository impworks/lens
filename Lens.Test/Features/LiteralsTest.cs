using System;
using System.Collections.Generic;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class LiteralsTest : TestBase
    {
        [Test]
        public void IntLiteral()
        {
            Test("1337", 1337);
        }

        [Test]
        public void LongLiteral()
        {
            Test("4294967294L", 4294967294);
        }

        [Test]
        public void CharLiteral()
        {
            Test("'x'", 'x');
        }

        [Test]
        public void CharLiteralEscapes()
        {
            Test(@"'\t'", '\t');
            Test(@"'\n'", '\n');
            Test(@"'\r'", '\r');
            Test(@"'\''", '\'');
        }

        [Test]
        public void CharLiteralError()
        {
            TestError("'abc'", LexerMessages.IncorrectCharLiteral);
            TestError("''", LexerMessages.IncorrectCharLiteral);
        }

        [Test]
        public void CharLiteralEscapeError()
        {
            TestError(@"'\x'", LexerMessages.UnknownEscape);
        }

        [Test]
        public void StringLiteral()
        {
            Test(@"""testy""", "testy");
        }

        [Test]
        public void StringLiteralEscapes()
        {
            Test(@"""\n""", "\n");
            Test(@"""\t""", "\t");
            Test(@"""\r""", "\r");
            Test(@"""\""""", "\"");
            Test(@"""a\nb""", "a\nb");
        }

        [Test]
        public void StringEscapedLiteralError()
        {
            TestError(@"""a\x""", LexerMessages.UnknownEscape);
        }

        [Test]
        public void StringVerbatimLiteral()
        {
            Test("@\"hello\"", "hello");
        }

        [Test]
        public void StringVerbatimLiteralBackslash()
        {
            Test("@\"hello \\test\"", "hello \\test");
        }

        [Test]
        public void StringVerbatimLiteralEscapeQuote()
        {
            Test("@\"hello \"\" world\"", "hello \" world");
        }

        [Test]
        public void StringLiteralUnclosedError()
        {
            TestError(@"""hello", LexerMessages.UnclosedString);
        }

        [Test]
        public void StringVerbatimLiteralUnclosedError()
        {
            TestError("@\"hello", LexerMessages.UnclosedString);
        }

        [Test]
        public void StringVerbatimLiteralIncorrectError()
        {
            TestError("@hello", LexerMessages.UnknownLexem);
        }

        [Test]
        public void FloatLiteral()
        {
            Test("1.337f", 1.337f);
        }

        [Test]
        public void DoubleLiteral()
        {
            Test("1.337", 1.337);
        }

        [Test]
        public void DecimalLiteral()
        {
            Test("1.337M", 1.337M);
        }

        [Test]
        public void BoolLiteral()
        {
            Test("true", true);
        }

        [Test]
        public void UnitLiteral()
        {
            Test("()", null);
        }

        [Test]
        public void ArrayLiteral()
        {
            Test("new [1; 2; 3]", new[] {1, 2, 3});
        }

        [Test]
        public void TupleLiteral()
        {
            Test(@"new (1; true; ""hello"")", Tuple.Create(1, true, "hello"));
        }

        [Test]
        public void ListLiteral()
        {
            Test(@"new [[1; 42; 1337]]", new List<int> {1, 42, 1337});
        }

        [Test]
        public void DictLiteral()
        {
            Test("new { 1 => true; 2 => false; 42 => true }", new Dictionary<int, bool> {{1, true}, {2, false}, {42, true}});
        }

        [Test]
        public void SizedArray()
        {
            Test("new int[2]", new int[2]);
        }

        [Test]
        public void EmptyArrayError()
        {
            TestError("new []", ParserMessages.ArrayItem);
        }

        [Test]
        public void EmptyTupleError()
        {
            TestError("new ()", ParserMessages.TupleItem);
        }

        [Test]
        public void TooLargeTupleError()
        {
            TestError("new (1;2;3;4;5;6;7;8;9;10)", CompilerMessages.TupleTooManyArgs);
        }

        [Test]
        public void EmptyListError()
        {
            TestError("new [[]]", ParserMessages.ListItem);
        }

        [Test]
        public void EmptyDictionaryError()
        {
            TestError("new {}", ParserMessages.DictionaryItem);
        }

        [Test]
        public void NulledArrayError()
        {
            TestError("new [null; null]", CompilerMessages.ArrayTypeUnknown);
        }

        [Test]
        public void NulledListError()
        {
            TestError("new [[null; null]]", CompilerMessages.ListTypeUnknown);
        }

        [Test]
        public void NulledDictionaryError1()
        {
            TestError("new {null => 1; null => 2}", CompilerMessages.DictionaryTypeUnknown);
        }

        [Test]
        public void NulledDictionaryError2()
        {
            TestError("new {1 => null; 2 => null}", CompilerMessages.DictionaryTypeUnknown);
        }

        [Test]
        public void LiteralInvocation()
        {
            var src = @"
let zeros = ""000000""
zeros.Substring 0 2
";
            Test(src, "00");
        }
    }
}