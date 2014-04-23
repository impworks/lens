using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
	[TestFixture]
	internal class LambdasInferenceTest : TestBase
	{
		[Test]
		public void Linq1()
		{
			var src = @"new [1; 2; 3; 4; 5].Where (a -> a > 2)";
			Test(src, new[] { 3, 4, 5 });
		}

		[Test]
		public void Linq2()
		{
			var src = @"
Enumerable::Range 1 10
    |> Where x -> x % 2 == 0
    |> Select x -> x * 2";

			var result = new[] { 4, 8, 12, 16, 20 };
			Test(src, result);
		}

		[Test]
		public void LambdaImplicitType1()
		{
			var src = @"
fun process:int (data:int[] check:Predicate<int>) ->
    var count = 0
    for x in data do
        if check x then count = count + 1
    count

process
    <| new [1; 2; 3; 4; 5]
    <| x -> x.odd()
";
			Test(src, 3);
		}

		[Test]
		public void LambdaImplicitType2()
		{
			var src = @"
fun collect:string[] (act:Func<string, int, string~>) ->
    let data = act ""test"" 5
    data
        |> Reverse ()
        |> ToArray ()

collect
    <| (str count) ->
        Enumerable::Repeat str count
            |> Select (x i) -> x + (i+1).ToString()
";
			Test(src, new[] { "test5", "test4", "test3", "test2", "test1" });
		}

		[Test]
		public void LambdaComposition()
		{
			var src = @"
let coeff = 2
let fx = int::Parse<string> :> (x -> x + coeff)
fx ""5""
";

			Test(src, 7);
		}

		[Test]
		public void LambdaCompositionInsanity()
		{
			var src = @"
let multiplier = (x:int y:int) -> x * y
let inv = (a:string b:string) -> b + a

// partially apply multiplier
let doubler = multiplier 2 _

// compose functions together
let invParse = inv :> int::Parse :> doubler :> (x -> println x)

invParse ""1"" ""2""
";
			Test(src, null);
		}

		[Test]
		public void LambdaVarAssignment()
		{
			var src = @"
var fx : Func<string, int, bool>
fx = (data count) -> data.Length > count
new [
    fx ""test"" 3
    fx ""test"" 5
]
";
			Test(src, new [] { true, false });
		}

		[Test]
		public void LambdaFieldAssignment()
		{
			var src = @"
record Test
    Fx : Func<int, string>

var holder = new Test ()
holder.Fx = a -> ""test"" + ((a * 2).ToString ())
holder.Fx 21
";
			Test(src, "test42");
		}

		[Test]
		public void LambdaIndexAssignment()
		{
			var src = @"
var fxs = new [ ((x:int y:int) -> x + y) ]
fxs[0] = (x y) -> x * y
fxs[0] 2 3
";
			Test(src, 6);
		}

		[Test]
		public void LambdaConstructor()
		{
			var src = @"
record Test
    Fx : Func<int, string>

var holder = new Test (a -> ""test"" + ((a * 2).ToString ()))
holder.Fx 21
";
			Test(src, "test42");
		}

		[Test]
		public void LambdaCast1()
		{
			var src = @"
var test = ((a b) -> a + b) as Func<int, int, int>
test 1 2
";
			Test(src, 3);
		}

		[Test]
		public void LambdaCast2()
		{
			var src = @"
var fx = (x -> x % 3 <> 0) as Predicate<int>
var data = new [1; 2; 3; 4; 5; 6]
Array::FindAll data fx
";
			Test(src, new [] { 1, 2, 4, 5 });
		}

		[Test]
		public void LambdaUninferred()
		{
			var src = @"
var test = (a b) -> a + b
test 1 2
";
			TestError(src, CompilerMessages.LambdaArgTypeUnknown);
		}

		[Test]
		public void LambdaArgsTypeMismatch()
		{
			var src = @"
fun invoker:string (act:Func<int,int,int>) ->
    fmt ""result = {0}"" (act ""1"" 2)

invoker ((x y) -> x + y)
";
			TestError(src, CompilerMessages.ArgumentTypeMismatch);
		}

		[Test]
		public void LambdaArgsCountMismatch()
		{
			var src = @"
fun invoker:string (act:Func<int,int>) ->
    fmt ""result = {0}"" (act 1 2)

invoker (x -> x + 1)
";
			TestError(src, CompilerMessages.DelegateArgumentsCountMismatch);
		}
	}
}
