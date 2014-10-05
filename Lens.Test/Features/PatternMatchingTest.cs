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
		public void TypeCheck()
		{
			var src = @"
fun getName:string (obj:object) ->
    match obj with
        case x:string then ""string""
        case x:int then ""int""
        case x:bool then ""bool""
        case _ then ""wtf""

new [1; ""test""; 1.3; true]
    |> Select getName
    |> ToArray ()";

			Test(src, new [] { "int", "string", "wtf", "bool"});
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

examples
    |> Select describe
    |> ToArray ()
";

			Test(src, new [] { "1:1", "empty", "1:<10", "2", "1:smth", "long" });
		}

		[Test]
		public void ArraySubsequence1()
		{
			var src = @"
match new[1; 2; 3; 4] with
    case [...x; _; 4] then x";

			Test(src, new [] {1, 2} );
		}

		[Test]
		public void ArraySubsequence2()
		{
			var src = @"
match new[1; 2; 3; 4] with
    case [_; ...x; _] then x";

			Test(src, new[] { 2, 3 });
		}

		[Test]
		public void ArraySubsequence3()
		{
			var src = @"
match new[1; 2; 3; 4] with
    case [1; _; ...x] then x";

			Test(src, new[] { 3, 4 });
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

points
    |> Select x -> describe x
    |> ToArray ()
";

			Test(src, new [] { "half-zero", "(2;3)", "zero" });
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

exprs
    |> Select describe
    |> ToArray ()
";

			Test(src, new[]{ "str='test'", "int=1", "unknown", "sum=5"});
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

new[12;10;6;5;0]
    |> Select describe
    |> ToArray ()
";
			Test(src, new [] { "other", "<=10", "<=10", "<=5", "<=5" });
		}
	}
}
