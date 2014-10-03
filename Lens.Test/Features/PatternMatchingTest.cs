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
    case x when x % 2 == 0 then ""even""
    case _ then ""odd""";

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
    |> Select x -> getName x
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
    |> Select x -> describe x
    |> ToArray ()
";

			Test(src, new [] { "1:1", "empty", "1:<10", "2", "1:smth", "long" });
		}
	}
}
