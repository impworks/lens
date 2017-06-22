using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class PatternMatchingTest : TestBase
    {
        [Test]
        public void NumericLiterals()
        {
            var src = @"
match 2 with
    case 1 then ""one""
    case 2 then ""two""";

            Test(src, "two");
        }

        [Test]
        public void StringLiterals()
        {
            var src = @"
match ""one"" with
    case ""one"" then 1
    case ""two"" then 2";

            Test(src, 1);
        }

        [Test]
        public void BooleanLiterals()
        {
            var src = @"
match true with
    case true then ""true""
    case false then ""false""";

            Test(src, "true");
        }

        [Test]
        public void DefaultCatch()
        {
            var src = @"
match 2 with
    case 1 then ""one""
    case _ then ""other""";

            Test(src, "other");
        }

        [Test]
        public void MultipleCases()
        {
            var src = @"
match 1 with
    case 1 | 2 then ""two or less""
    case _ then ""other""";

            Test(src, "two or less");
        }

        [Test]
        public void WhenGuard()
        {
            var src = @"
match 4 with
    case x when x % 2 == 1 then ""odd""
    case _ then ""even""";

            Test(src, "even");
        }

        [Test]
        public void WhenGuard2()
        {
            var src = @"
match 1 with
    case 1 when false then ""impossible""
    case _ then ""one""";

            Test(src, "one");
        }

        [Test]
        public void TypeCheck()
        {
            var src = @"
fun getName:string (obj:object) ->
    match obj with
        case x:string then ""string""
        case x:int then ""int""
        case x:bool then ""bool""
        case _ then ""wtf""

new [1; ""test""; 1.3; true].Select getName";

            Test(src, new[] {"int", "string", "wtf", "bool"});
        }

        [Test]
        public void Array()
        {
            var src = @"
fun describe:string (arr:int[]) ->
    match arr with
        case [] then ""empty""
        case [1] then ""1:1""
        case [x] when x < 10 then ""1:<10""
        case [_] then ""1:smth""
        case [_;_] then ""2""
        case _ then ""long""

var examples = new [
    new [1]
    new int[0]
    new [4]
    new [1; 2]
    new [30]
    new [1; 2; 3]
]

examples.Select describe
";

            Test(src, new[] {"1:1", "empty", "1:<10", "2", "1:smth", "long"});
        }

        [Test]
        public void ArraySubsequence1()
        {
            var src = @"
match new[1; 2; 3; 4] with
    case [...x; _; 4] then x";

            Test(src, new[] {1, 2});
        }

        [Test]
        public void ArraySubsequence2()
        {
            var src = @"
match new[1; 2; 3; 4] with
    case [_; ...x; _] then x";

            Test(src, new[] {2, 3});
        }

        [Test]
        public void ArraySubsequence3()
        {
            var src = @"
match new[1; 2; 3; 4] with
    case [1; _; ...x] then x";

            Test(src, new[] {3, 4});
        }

        [Test]
        public void ArraySubsequence4()
        {
            var src = @"
fun len:int (arr:int[]) ->
    match arr with
        case [] then 0
        case [_; ...x] then 1 + (len x)

len new [1; 3; 5; 7]";

            Test(src, 4);
        }

        [Test]
        public void EnumerableSubsequence()
        {
            var src = @"
var seq = 1.to 10
match seq with
    case [2; 3; ...x] then fmt ""fail {0}"" (x.Count())
    case [1; 2; ...x] then fmt ""ok {0}"" (x.Count())";

            Test(src, "ok 8");
        }

        [Test]
        public void Record()
        {
            var src = @"
record MyPoint
    X : int
    Y : int

fun describe:string (pt: object) ->
    match pt with
        case MyPoint(X = 0; Y = 0) then ""zero""
        case MyPoint(X = 0) | MyPoint(Y = 0) then ""half-zero""
        case MyPoint(X = x; Y = y) then fmt ""({0};{1})"" x y

var points = new [
    new MyPoint 0 1
    new MyPoint 2 3
    new MyPoint ()
]

points.Select (x -> describe x)
";

            Test(src, new[] {"half-zero", "(2;3)", "zero"});
        }

        [Test]
        public void Type()
        {
            var src = @"
type Expr
    IntExpr of int
    StringExpr of string
    SumExpr of Tuple<int, int>
    OtherExpr

fun describe:string (expr:Expr) ->
    match expr with
        case IntExpr of x then fmt ""int={0}"" x
        case StringExpr of x then fmt ""str='{0}'"" x
        case SumExpr of (x; y) then fmt ""sum={0}"" (x+y)
        case _ then ""unknown""

var exprs = new [
    StringExpr ""test""
    IntExpr 1
    OtherExpr
    SumExpr (Tuple::Create 2 3)
]

exprs.Select describe
";

            Test(src, new[] {"str='test'", "int=1", "unknown", "sum=5"});
        }

        [Test]
        public void Range()
        {
            var src = @"
fun describe:string (value:int) ->
    match value with
        case 0..5 then ""<=5""
        case 6..10 then ""<=10""
        case _ then ""other""

new [12; 10; 6; 5; 0].Select describe
";
            Test(src, new[] {"other", "<=10", "<=10", "<=5", "<=5"});
        }

        [Test]
        public void KeyValue()
        {
            var src = @"
fun describe:string (value:KeyValuePair<string, int[]>) ->
    match value with
        case x => [] then fmt ""{0} => empty"" x
        case x => [y] then fmt ""{0} => {1}"" x y
        case x => [_; ..._] then fmt ""{0} => many"" x

let values = new {
    ""a"" => new [1; 2; 3]
    ""b"" => new int[0]
    ""c"" => new [42]
}

values.Select (x -> describe x)
";
            Test(src, new[] {"a => many", "b => empty", "c => 42"});
        }

        [Test]
        public void Null()
        {
            var src = @"
fun describe:string (obj:object) ->
    match obj with
        case null then ""is null""
        case _ then ""is not null""

new [""test""; null ].Select (x -> describe x)
";
            Test(src, new[] {"is not null", "is null"});
        }

        [Test]
        public void Regex()
        {
            var src = @"
fun describe:string (str:string) ->
    match str with
        case #^\d{1}$# then ""1""
        case #^\d{2}$# then ""2""
        case #^\d{3,5}$# then ""3-5""
        case _ then ""other""

new [""42""; ""1""; ""321567""; ""1234""].Select describe
";
            Test(src, new[] {"2", "1", "other", "3-5"});
        }

        [Test]
        public void RegexNamedGroups()
        {
            var src = @"
match ""[world-hello]"" with
    case #\[(?<fst>\w+)-(?<snd>\w+)\]# then snd + "" "" + fst
";
            Test(src, "hello world");
        }

        [Test]
        public void RegexNamedGroupsWithConverters()
        {
            var src = @"
match ""[23-19]"" with
    case #\[(?<fst:int>\d+)-(?<snd:int>\d+)\]# then fst + snd
";
            Test(src, 42);
        }

        [Test]
        public void RegexNamedGroupsTryParseNoThrow()
        {
            var src = @"
match ""13,37"" with
    case #(?<value:int>\d+,\d+)# then ""int""
    case #(?<value:float>\d+,\d+)# then ""float""
";
            Test(src, "float");
        }

        [Test]
        public void RegexModifiers()
        {
            var src = @"
fun describe:int (str:string) ->
    match str with
        case #^[a-z]+$# then 1
        case #^[a-z]+$#i then 2
        case _ then 3

new [""ABC""; ""123""; ""hello""].Select describe";

            Test(src, new[] {2, 3, 1});
        }

        [Test]
        public void RegexEscape()
        {
            var src = @"
match ""a#b"" with
    case #(?<one>[a-z])##(?<two>[a-z])# then two + one
    case _ then ""oops""
";
            Test(src, "ba");
        }

        [Test]
        public void RuleTypeMismatchError()
        {
            var src = @"
match 1 with
    case true then true
";
            TestError(src, CompilerMessages.PatternTypeMismatch);
        }

        [Test]
        public void RuleTypeImpossibleError()
        {
            var src = @"
match 1 with
    case x:string then true
";
            TestError(src, CompilerMessages.PatternTypeMatchImpossible);
        }

        [Test]
        public void RuleUnreachableError1()
        {
            var src = @"
match 1 with
    case x then true
    case _ then false
";
            TestError(src, CompilerMessages.PatternUnreachable);
        }

        [Test]
        public void RuleUnreachableError2()
        {
            var src = @"
match 1 with
    case x:object then true
    case y:int then false
";
            TestError(src, CompilerMessages.PatternUnreachable);
        }

        [Test]
        public void RuleRecordTypeError()
        {
            var src = @"
use System.Drawing

var pt = new Point ()
match 1 with
    case Point(X = x) then true
";
            TestError(src, CompilerMessages.PatternNotValidRecord);
        }

        [Test]
        public void RuleTypeError()
        {
            var src = @"
use System.Drawing

var pt = new Point ()
match pt with
    case Point of x then true
";
            TestError(src, CompilerMessages.PatternNotValidType);
        }

        [Test]
        public void RuleTypeLabelError()
        {
            var src = @"
type Item
    Point
    Circle

var pt = Point
match pt with
    case Point of x then true
";
            TestError(src, CompilerMessages.PatternTypeNoTag);
        }

        [Test]
        public void RuleRecordFieldNotFoundError()
        {
            var src = @"
record Point
    X : int
    Y : int

var pt = new Point 1 2
match pt with
    case Point(Z = 1) then true
";
            TestError(src, CompilerMessages.PatternRecordNoField);
        }

        [Test]
        public void RuleRecordFieldDuplicatedError()
        {
            var src = @"
record Point
    X : int
    Y : int

var pt = new Point 1 2
match pt with
    case Point(X = 1; X = 2) then true
";
            TestError(src, CompilerMessages.PatternRecordFieldDuplicated);
        }

        [Test]
        public void RuleRangeNotNumericError()
        {
            var src = @"
match 1 with
    case ""a""..""z"" then true
";
            TestError(src, CompilerMessages.PatternRangeNotNumeric);
        }

        [Test]
        public void RuleArraySubsequenceError()
        {
            var src = @"
match new [1] with
    case [...x; ...y] then 1
";
            TestError(src, CompilerMessages.PatternArraySubsequences);
        }

        [Test]
        public void RuleIEnumerableSubsequenceError()
        {
            var src = @"
var expr = Enumerable::Range 1 30
match expr with
    case [...x; y] then 1
";
            TestError(src, CompilerMessages.PatternSubsequenceLocation);
        }

        [Test]
        public void RuleNameSetsMismatchError()
        {
            var src = @"
var expr = Enumerable::Range 1 30
match new [1] with
    case [x] | [x; y] then 1
";
            TestError(src, CompilerMessages.PatternNameSetMismatch);
        }

        [Test]
        public void RuleNameDuplicatedError()
        {
            var src = @"
match new [1] with
    case [x; x] then 1
";
            TestError(src, CompilerMessages.PatternNameDuplicated);
        }

        [Test]
        public void RuleNameTypeMismatchError()
        {
            var src = @"
record MyRecord
    A : int
    B : string

var obj = new MyRecord 1 ""test""
match obj with
    case MyRecord(A = 1; B = x) | MyRecord(B = ""test""; A = x) then 1
";
            TestError(src, CompilerMessages.PatternNameTypeMismatch);
        }

        [Test]
        public void RegexSyntaxError()
        {
            var src = @"
match ""123"" with
    case #[a# then true
";
            TestError(src, CompilerMessages.RegexSyntaxError);
        }

        [Test]
        public void RegexUnknownModifier()
        {
            var src = @"
match ""123"" with
    case #\d+#y then true
";
            TestError(src, CompilerMessages.RegexUnknownModifier);
        }

        [Test]
        public void RegexDuplicateModifier()
        {
            var src = @"
match ""123"" with
    case #\d+#ii then true
";
            TestError(src, CompilerMessages.RegexDuplicateModifier);
        }

        [Test]
        public void RegexConverterTypeNotFound()
        {
            var src = @"
match ""123"" with
    case #(?<test:MyType>123)# then true
";
            TestError(src, CompilerMessages.RegexConverterTypeNotFound);
        }

        [Test]
        public void RegexConverterTypeIncompatible()
        {
            var src = @"
match ""123"" with
    case #(?<test:object>123)# then true
";
            TestError(src, CompilerMessages.RegexConverterTypeIncompatible);
        }

        [Test]
        public void RegexUnderscoreGroupName()
        {
            var src = @"
match ""123"" with
    case #(?<_>123)# then true
";
            TestError(src, CompilerMessages.UnderscoreNameUsed);
        }
    }
}