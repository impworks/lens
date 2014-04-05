using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
	[TestFixture]
	class UserDeclarationsTest : TestBase
	{

		[Test]
		public void FuncDeclareAndInvoke()
		{
			var src = @"
fun test:int -> 10
test ()";
			Test(src, 10);
		}

		[Test]
		public void FuncRecursive()
		{
			var src = @"fun fact:int (a:int) -> if a == 0 then 1 else 1 * fact(a-1)";
			Test(src, null);
		}

		[Test]
		public void Algebraic1()
		{
			var src = @"
type TestType
    Value of int

(Value 1).Tag";

			Test(src, 1);
		}

		[Test]
		public void Algebraic2()
		{
			var src = @"
type TestType
    Small of int
    Large of int

var a = Small 1
var b = Large 100
a.Tag + b.Tag";

			Test(src, 101);
		}

		[Test]
		public void Algebraic3()
		{
			var src = @"
type TestType
    Small of int
    Large of int

fun part:TestType (x:int) ->
    if x > 100 then
        (Large x) as TestType
    else
        Small x

var a = part 10
new [ a is TestType; a is Small; a is Large ]";

			Test(src, new[] { true, true, false });
		}

		[Test]
		public void Algebraic4()
		{
			var src = @"
type Test
    Value1
    Value2 of Test
    Value3 of Tuple<Test, string>

let v1 = Value1
let v2 = Value2 v1
let v3 = Value3 (new(v2 as Test; ""hello""))
new [v1 is Test; v2 is Test; v3 is Test]";

			Test(src, new[] { true, true, true });
		}

		[Test]
		public void Records1()
		{
			var src = @"
record Holder
    A : int
    B : int

var a = new Holder 2 3
a.A * a.B
";

			Test(src, 6);
		}

		[Test]
		public void Records2()
		{
			var src = @"
record First
    A : int

record Second
    B : int

var a = new First 2
var b = new Second 3
a.A * b.B
";

			Test(src, 6);
		}

		[Test]
		public void RefFunction1()
		{
			var src = @"
var x = 0
int::TryParse ""100"" ref x
x";

			Test(src, 100);
		}

		[Test]
		public void RefFunction2()
		{
			var src = @"
fun test (a:int x:ref int) -> x = a * 2

var result = 0
test 21 (ref result)
result
";

			Test(src, 42);
		}

		[Test]
		public void GenericWithCustom1()
		{
			var src = @"
record MyRecord
    Value : int

var r1 = new MyRecord 1
var r2 = new MyRecord 2
var res = new (r1; r2)
res.Item1.Value + res.Item2.Value
";

			Test(src, 3);
		}

		[Test]
		public void GenericWithCustom2()
		{
			var src = @"
record MyRecord
    Value : int

var list = new [[new MyRecord 1; new MyRecord 2; new MyRecord 3]]
list.Count
";
			Test(src, 3);
		}

		[Test]
		public void GenericWithCustom3()
		{
			var src = @"
record MyRecord
    Value : int

var list = new [[new MyRecord 1; new MyRecord 2; new MyRecord 3]]
list[1].Value + list[2].Value
";
			Test(src, 5);
		}

		[Test]
		public void BuiltInComparison1()
		{
			var src = @"
record MyRecord
    Integer : int
    Stringy : string

var r1 = new MyRecord 4 ""lol""
var r2 = new MyRecord (2 + 2) (""l"" + ""ol"")
r1 == r2
";

			Test(src, true, true);
		}

		[Test]
		public void BuiltInComparison2()
		{
			var src = @"
type MyType
    Test of string

var r1 = Test ""hi""
var r2 = Test (""HI"".ToLower ())
r1 == r2
";

			Test(src, true);
		}

		[Test]
		public void CustomTypeLinq1()
		{
			var src = @"
record Store
    Name : string
    Value : int

var found = new [new Store ""a"" 1; new Store ""b"" 2; new Store ""c"" 3]
    |> Where (x:Store -> x.Value < 3)
    |> OrderByDescending (x:Store -> x.Value)
    |> First ()

found.Name
";
			Test(src, "b");
		}

		[Test]
		public void CustomTypeLinq2()
		{
			var src = @"
record Store
    Value : int

var data = new [new Store 1; new Store 1; new Store 40]
data.Sum (x:Store -> x.Value)
";
			Test(src, 42);
		}

		[Test]
		public void PureFunc0()
		{
			var src = @"
pure fun Test:int ->
    println ""calculated""
    42

new [Test (); Test ()]";

			Test(src, new[] { 42, 42 });
		}

		[Test]
		public void PureFunc1()
		{
			var src = @"
pure fun Test:int (x:int) ->
    println ""calculated""
    x * 2

new [
    Test 1
    Test 2
    Test 2
    Test 3
    Test 1
]
";

			Test(src, new[] { 2, 4, 4, 6, 2 });
		}

		[Test]
		public void PureFunc2()
		{
			var src = @"
pure fun Sum:int (x:int y:int) ->
    println ""calculated""
    x + y

new [
    Sum 1 1
    Sum 1 2
    Sum 2 3
    Sum 1 1
    Sum 2 3
]";

			Test(src, new[] { 2, 3, 5, 2, 5 });
		}

		[Test]
		public void VariadicFunction1()
		{
			var src = @"
fun mySum:int (data:int...) ->
    var sum = 0
    for curr in data do
        sum = sum + curr
    sum

new [mySum 1; mySum 1 2; mySum 1 2 3 4 5; mySum (new [1; 2; 3])]
";
			Test(src, new[] { 1, 3, 15, 6 });
		}

		[Test]
		public void VariadicFunction2()
		{
			var src = @"
fun cat:string (data:object...) ->
    string::Join
        <| "";""
        <| data.Select (x:object -> x.ToString())

cat 1 2 true ""test""
";
			Test(src, "1;2;True;test");
		}

		[Test]
		public void VariadicFunctionFail1()
		{
			var src = @"
fun mySum:int (data:ref int...) ->
    var sum = 0
    for curr in data do
        sum = sum + curr
    sum
";
			TestError(src, ParserMessages.VariadicByRef);
		}

		[Test]
		public void VariadicFunctionFail2()
		{
			var src = @"
fun mySum:int (x:string... data:int...) ->
    var sum = 0
    for curr in data do
        sum = sum + curr
    sum
";
			TestError(src, CompilerMessages.VariadicArgumentNotLast);
		}

		[Test]
		public void VariadicFunctionFail3()
		{
			var src = @"
let s = (x:int...) -> x.Sum ()
s 1 2 3
";
			TestError(src, CompilerMessages.VariadicArgumentLambda);
		}
	}
}
