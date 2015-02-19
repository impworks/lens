using System;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
	[TestFixture]
	class ControlStructuresTest : TestBase
	{
		[Test]
		public void Condition1()
		{
			var src = @"
var x = 1
if 1 < 2 then
    x = 2
x
";
			Test(src, 2);
		}

		[Test]
		public void Condition2()
		{
			var src = @"
var x = 1
if 1 > 2 then
    x = 2
x
";
			Test(src, 1);
		}

		[Test]
		public void Condition3()
		{
			var src = @"
var x = 1
if 1 > 2 then
    x = 2
else
    x = 3
x
";
			Test(src, 3);
		}

		[Test]
		public void ConditionAssignment()
		{
			var src = @"
let x = if 1 > 2 then ""a"" else ""b""
x";

			Test(src, "b");
		}

		[Test]
		public void ConditionTypeInference()
		{
			var src = @"(new [ if 1 > 2 then ""a"" else true ]).GetType()";
			Test(src, typeof (object[]));
		}

		[Test]
		public void ConditionError1()
		{
			var src = @"if 1 then 2";
			TestError(src, CompilerMessages.ConditionTypeMismatch);
		}

		[Test]
		public void ConditionError2()
		{
			var src = @"var x = if true then 2";
			TestError(src, CompilerMessages.ExpressionVoid);
		}

		[Test]
		public void Loop()
		{
			var src = @"
var a = 1
var idx = 0
while idx < 5 do
    a = a * 2
    idx = idx + 1
a";

			Test(src, 32);
		}

		[Test]
		public void LoopResult()
		{
			var src = @"
var a = 1
var res = while a < 10 do
    a = a * 2
    a
res";
			Test(src, 16);
		}

		[Test]
		public void NotNestedLoop()
		{
			var src = @"
var data = new [1; 2; 3; 4; 5]
var sum = 0
for x in data do
    sum = sum + x
sum
";

			Test(src, 15);
		}

		[Test]
		public void NestedLoop()
		{
			var src = @"
var data = new [
    new [1; 2; 3]
    new [4; 5; 6]
    new [7; 8; 9]
]

var result = 0
for row in data do
    for x in row do
        result = result + x

result
";
			Test(src, 45);
		}

		[Test]
		public void LoopError()
		{
			var src = @"while 1 do 2";
			TestError(src, CompilerMessages.ConditionTypeMismatch);
		}

		[Test]
		public void ForLoop1()
		{
			var src = @"
var a = """"
for i in 5..1 do
    a = a + i.ToString ()
a";

			Test(src, "5432");
		}

		[Test]
		public void ForLoop2()
		{
			var src = @"
for x in new [1; 2; 3; 4; 5] do
    println ""and {0}"" x
    x
";

			Test(src, 5);
		}

		[Test]
		public void ForLoop3()
		{
			var src = @"
for x in new [1; 2; 3; 4; 5] do
    println ""and {0}"" x";

			Test(src, null);
		}

		[Test]
		public void ForLoop4()
		{
			var src = @"
for x in Enumerable::Range 1 5 do
    println ""and {0}"" x";

			Test(src, null);
		}

		[Test]
		public void ForLoop5()
		{
			var src = @"
for x in new [[1; 2; 3; 4; 5]] do
    println ""and {0}"" x";

			Test(src, null);
		}

		[Test]
		public void ForLoop6()
		{
			var src = @"
var x = 0
for y in 1..1 do
    x = x + 1
x
";
			Test(src, 0);
		}

		[Test]
		public void ForLoopError1()
		{
			var src = @"
for x in ""a""..""b"" do print x
";
			TestError(src, CompilerMessages.ForeachRangeNotInteger);
		}

		[Test]
		public void ForLoopError2()
		{
			var src = @"
for x in 1..1.5 do print x
";
			TestError(src, CompilerMessages.ForeachRangeTypeMismatch);
		}

		[Test]
		public void ForLoopError3()
		{
			var src = @"
for x in true do print x
";
			TestError(src, CompilerMessages.TypeNotIterable);
		}

		[Test]
		public void ThrowNew()
		{
			var src = "throw new NotImplementedException ()";
			Assert.Throws<NotImplementedException>(() => Compile(src));
		}

		[Test]
		public void ThrowExisting()
		{
			var src = @"
var ex = new NotImplementedException ()
throw ex";

			Assert.Throws<NotImplementedException>(() => Compile(src));
		}

		[Test]
		public void ThrowError()
		{
			var src = "throw 1";
			TestError(src, CompilerMessages.ThrowTypeNotException);
		}

		[Test]
		public void TryCatch()
		{
			var src = @"
var msg = 1
try
    var zero = 0
    1 / zero
catch ex:DivideByZeroException
    msg = 2
msg
";
			Test(src, 2);
		}

		[Test]
		public void TryCatchError1()
		{
			var src = @"
try
    println true
catch x:int
    println false";

			TestError(src, CompilerMessages.CatchTypeNotException);
		}

		[Test]
		public void TryCatchError2()
		{
			var src = @"
try
    println true
catch x:DivideByZeroException
    println false
catch y:DivideByZeroException
    println false";

			TestError(src, CompilerMessages.CatchTypeDuplicate);
		}

		[Test]
		public void TryCatchError3()
		{
			var src = @"
try
    println true
catch x:Exception
    println false
catch y:DivideByZeroException
    println false";

			TestError(src, CompilerMessages.CatchClauseUnreachable);
		}

		[Test]
		public void TryFinally()
		{
			var src = @"
var a : int
var b : int
try
    var c = 0
    1 / c
catch
    b = 2
finally
    a = 1

new [a; b]";

			Test(src, new[] { 1, 2 });
		}

		[Test]
		public void Using()
		{
			var src = @"
var x = 1
let disp = new Lens.Test.Features.SampleDisposable (-> x = 2)
using disp do
    x = 3
x
";
			Test(src, 2);
		}

		[Test]
		public void Using2()
		{
			var src = @"
var x = 1
let disp = new Lens.Test.Features.SampleDisposable (-> x = 2)
using disp2 = disp do
    disp2 = null
x
";
			Test(src, 2);
		}

		[Test]
		public void UsingError1()
		{
			var src = @"
var x = 1
using a = 2 do
    x = 2
";
			TestError(src, CompilerMessages.ExpressionNotIDisposable);
		}

		[Test]
		public void UsingError2()
		{
			var src = @"
var x = 1
using x = new Lens.Test.Features.SampleDisposable (-> x = 2) do
    x = 2
";
			TestError(src, CompilerMessages.VariableDefined);
		}
	}

	public class SampleDisposable : IDisposable
	{
		public SampleDisposable(Action act)
		{
			_Action = act;
		}

		private readonly Action _Action;

		public void Dispose()
		{
			_Action();
		}
	}
}
