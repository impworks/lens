using System;
using System.Collections.Generic;
using System.Text;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class OperatorsTest : TestBase
    {
        [Test]
        public void TypeofTest()
        {
            TestType<int>("typeof int");
            TestType<float>("typeof float");
            TestType<object>("typeof object");

            TestType<Dictionary<int, Exception>>("typeof Dictionary<int, System.Exception>");
        }

        [Test]
        public void DefaultTest()
        {
            Test("default int", 0);
            Test("default float", 0.0f);
            Test("default long", 0L);
            Test("default bool", false);

            Test("default string", null);

            Test("default int?", default(int?));
        }

        [Test]
        public void CastTest()
        {
            Test("0 as float", 0.0f);
            Test("0.0 as int", 0);
            Test("1.23 as long", 1L);
            Test("1 as int?", 1);
            Test("null as string", null);
            Test("null as int?", new int?());
        }

        [Test]
        public void NewObjectTest()
        {
            Assert.IsInstanceOf<StringBuilder>(Compile("new System.Text.StringBuilder ()"));
            Assert.IsInstanceOf<List<int>>(Compile("new List<int> ()"));

            Test("new Decimal 1", new Decimal(1));
            Test(@"new Uri ""http://test.ru""", new Uri("http://test.ru"));
            Test(@"new Tuple<int, string> 1 ""hello""", new Tuple<int, string>(1, "hello"));
        }

        [Test]
        public void ArithmeticsTest()
        {
            Test("1 + 2", 3, true);
            Test("13 + 0.37", 13.37, true);
            Test("1336.9 + 0.1", 1337, true);
            Test("(1336 as UInt32) + (1 as UInt32)", 1337, true);

            Test("43 - 1", 42, true);

            Test("21 * 2", 42, true);
            Test("1.5 * 1.5", 2.25, true);

            Test("84 / 2", 42, true);

            Test("92 % 50", 42, true);

            Test("2 ** 2", 4, true);
            Test("1.5 ** 5", 7.59375, true);

            Assert.Throws<LensCompilerException>(() => Compile("1 + (1 as UInt32)"));
            Assert.Throws<LensCompilerException>(() => Compile(@"1 + ""hello"""));
        }

        [Test]
        public void StringConcatTest()
        {
            Test(@"""a"" + ""b""", "ab", true);
            Test(@"""a"" + ""b"" + ""c""", "abc", true);
        }

        [Test]
        public void NegationTest()
        {
            Test("-1", -1, true);
            Test("-1.5", -1.5, true);

            var src = @"
let a = 1
-(a * 2)
";
            Test(src, -2, true);
        }

        [Test]
        public void OperatorPrecedenceTest()
        {
            Test("2 + 2 * 2", 6, true);
            Test("2 / 2 + 1", 2, true);
            Test("1 + 2 * 3 ** 4", 163, true);
        }

        [Test]
        public void BooleanOperatorsTest()
        {
            Test("true || true", true, true);
            Test("true || false", true, true);
            Test("false || true", true, true);
            Test("false || false", false, true);

            Test("true && true", true, true);
            Test("true && false", false, true);
            Test("false && true", false, true);
            Test("false && false", false, true);
        }

        [Test]
        public void XorTest()
        {
            Test("true ^^ true", false, true);
            Test("true ^^ false", true, true);
            Test("false ^^ true", true, true);
            Test("false ^^ false", false, true);

            Test("42 ^^ 1337", 1299, true);
        }

        [Test]
        public void IntegerBitOperatorsTest()
        {
            Test("1 | 2 | 4", 7);
            Test("1 | 3 | 6", 7);

            Test("1337 & 42", 40);

            Test("1337 ^ 42", 1299);
        }

        [Test]
        public void EnumBitOperatorsTest()
        {
            Test("StringComparison::Ordinal | StringComparison::CurrentCulture", StringComparison.Ordinal | StringComparison.CurrentCulture);
        }

        [Test]
        public void BitOperatorsTypeError()
        {
            TestError("1 & true", CompilerMessages.OperatorBinaryTypesMismatch);
            TestError("1.2f | 1.5f", CompilerMessages.OperatorBinaryTypesMismatch);
            TestError("new [1] | new [2]", CompilerMessages.OperatorBinaryTypesMismatch);
            TestError("1M | 2M", CompilerMessages.OperatorBinaryTypesMismatch);
        }

        [Test]
        public void InversionTest()
        {
            Test("not true", false, true);
            Test("not false", true, true);

            Test("not true || true", true, true);
        }

        [Test]
        public void ComparisonTest()
        {
            Test("1 == 1", true, true);
            Test("1 == 2", false, true);
            Test("1 <> 1", false, true);
            Test("1 <> 2", true, true);

            Test("1 == 1.0", true, true);
            Test("1 == 1.2", false, true);
            Test("1 <> 1.0", false, true);
            Test("1 <> 1.2", true, true);

            Test("1.0 == 1.0", true, true);
            Test("1.0 <> 1.0", false, true);

            Test("1 == (1 as int?)", true);
            Test("1 <> (1 as int?)", false);
            Test("(1 as int?) == (1 as int?)", true);
            Test("(1 as int?) <> (1 as int?)", false);

            Test("(1 as int?) == null", false);
            Test("(1 as int?) <> null", true);

            Test("null == null", true, true);
            Test("null == (new object ())", false);
        }

        [Test]
        public void GetIndexTest()
        {
            Test("(new [1; 2; 3])[1]", 2);
            Test("new [1; 2; 3][1]", 2);
            Test(@"(new [[""a""; ""b""; ""c""]])[1]", "b");
            Test(@"new [[""a""; ""b""; ""c""]][1]", "b");
            Test(@"(new { ""a"" => 1; ""b"" => 2})[""a""]", 1);
            Test(@"new { ""a"" => 1; ""b"" => 2}[""a""]", 1);
        }

        [Test]
        public void OverloadedOperators()
        {
            Test("1M + 2M", 3);
            Test("2M - 1M", 1);
            Test("2M * 2M", 4);
            Test("42M / 2M", 21);
            Test("100M % 3M", 1);
            Test("1M == 2M", false);
            Test("1M <> 2M", true);
            Test("1M < 2M", true);
            Test("1M <= 2M", true);
            Test("1M > 2M", false);
            Test("1M >= 2M", false);
        }

        [Test]
        public void OverloadedOperatorsCasting()
        {
            Test("1M + 1", 2M);
            Test("1 + 1M", 2M);
            Test("1M + 2L", 3M);
            Test("2L + 1M", 3M);
        }

        [Test]
        public void ArrayConcat()
        {
            Test("new [1; 2; 3] + new [4; 5; 6]", new[] {1, 2, 3, 4, 5, 6});
            Test(@"new [""A""; ""B""] + new [""D""; ""C""]", new[] {"A", "B", "D", "C"});
        }

        [Test]
        public void IEnumerableConcat()
        {
            Test(
                @"(new [1; 2; 3].Select (x -> x * 2)) + new [[8; 10]]",
                new[] {2, 4, 6, 8, 10}
            );
        }

        [Test]
        public void DictionaryConcat()
        {
            Test(
                @"new {1 => true; 2 => true } + new { 2 => false; 3 => false }",
                new Dictionary<int, bool> {{1, true}, {2, false}, {3, false}}
            );
        }

        [Test]
        public void StringRepetition()
        {
            Test(@"""abc"" * 2", "abcabc", true);
        }

        [Test]
        public void ArrayRepetition()
        {
            Test(@"new [1; 2; 3] * 3", new[] {1, 2, 3, 1, 2, 3, 1, 2, 3});
        }

        [Test]
        public void TypedSequenceRepetition()
        {
            Test(@"(1.to 3) * 2", new[] {1, 2, 3, 1, 2, 3});
        }

        [Test]
        public void UntypedSequenceRepetition()
        {
            var src = @"
var x = 1.to 3
(x as IEnumerable) * 2";

            Test(src, new[] {1, 2, 3, 1, 2, 3});
        }

        [Test]
        public void ShorthandAssignment1()
        {
            var src = @"
var x = 1
x += 2
x
";
            Test(src, 3);
        }

        [Test]
        public void ShorthandAssignment2()
        {
            var src = @"
var x = new [""a""; ""b""]
x[0] += ""c""
x
";
            Test(src, new[] {"ac", "b"});
        }

        [Test]
        public void ShorthandAssignment3()
        {
            var src = @"
record Point
    X : double
    Y : double

var pt = new Point ()
pt.X += 1.5
pt.Y += pt.X + 1
pt.X + pt.Y
";
            Test(src, 4);
        }

        [Test]
        public void ShorthandAssignment4()
        {
            var src = @"
let data = new [new [1]; new [2]; new [3]]
let getter = (x:int) -> data[x]
let indexOffset = 2
(getter 1)[indexOffset - 2] += 1
(getter 2)[indexOffset - indexOffset] += 2
data.Select (x -> x[0])
";
            Test(src, new[] {1, 3, 5});
        }

        [Test]
        public void ComplexConstants()
        {
            var src = @"
let x = 1.3M
let y = 1.2M
x + y";
            Test(src, 2.5M);
        }

        [Test]
        public void MissingOperandError()
        {
            TestError("/1", ParserMessages.UnknownStatement);
            TestError("1+", ParserMessages.ExpressionExpected);
        }

        private void TestType<T>(string src)
        {
            var obj = Compile(src);
            Assert.AreEqual(obj, typeof(T));
        }
    }
}