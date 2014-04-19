using System;
using NUnit.Framework;

namespace Lens.Test.Features
{
	[TestFixture]
	class ControlStructuresTest : TestBase
	{
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
		public void ForLoop1()
		{
			var src = @"
var a = """"
for i in 5..1 do
    a = a + i.ToString ()
a";

			Test(src, "54321");
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
