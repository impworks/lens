using System;
using System.Linq;
using NUnit.Framework;

namespace Lens.Test.Features
{
	[TestFixture]
	internal class StdlibTest : TestBase
	{
		[Test]
		public void FormatTest()
		{
			Test(@"fmt ""!{0}"" 1", "!1");
			Test(@"fmt ""!{0}!{1}"" 1 2", "!1!2");
			Test(@"fmt ""!{0}!{1}!{2}"" 1 2 3", "!1!2!3");
			Test(@"fmt ""!{0}!{1}!{2}!{3}"" 1 2 3 4", "!1!2!3!4");
			Test(@"fmt ""!{0}!{1}!{2}!{3}!{4}"" 1 2 3 4 5", "!1!2!3!4!5");
			Test(@"fmt ""!{0}!{1}!{2}!{3}!{4}!{5}"" 1 2 3 4 5 6", "!1!2!3!4!5!6");
			Test(@"fmt ""!{0}!{1}!{2}!{3}!{4}!{5}!{6}"" 1 2 3 4 5 6 7", "!1!2!3!4!5!6!7");
			Test(@"fmt ""!{0}!{1}!{2}!{3}!{4}!{5}!{6}!{7}"" 1 2 3 4 5 6 7 8", "!1!2!3!4!5!6!7!8");
			Test(@"fmt ""!{0}!{1}!{2}!{3}!{4}!{5}!{6}!{7}!{8}"" 1 2 3 4 5 6 7 8 9", "!1!2!3!4!5!6!7!8!9");
			Test(@"fmt ""!{0}!{1}!{2}!{3}!{4}!{5}!{6}!{7}!{8}!{9}"" 1 2 3 4 5 6 7 8 9 true", "!1!2!3!4!5!6!7!8!9!True");
		}

		[Test]
		public void FailTest()
		{
			var src = @"fail ""test""";
			Assert.Throws<Exception>(() => Compile(src));
		}

		[Test]
		public void TimesTest1()
		{
			var src = @"
var r = 1
5.times (-> r = r * 2)
r
";
			Test(src, 32);
		}

		[Test]
		public void TimesTest2()
		{
			var src = @"
var r = string::Empty
5.times (x -> r = r + x.ToString ())
r
";
			Test(src, "01234");
		}

        [Test]
        public void TimesTest3()
        {
            var src = @"
var r = new List<string> ()
new (2;2)
    |> times ((x y) -> r.Add (fmt ""{0}.{1}"" x y))
string::Join ""-"" r
";
            Test(src, "0.0-0.1-1.0-1.1");
        }

		[Test]
		public void RandTest1()
		{
			var src = "rand ()";
			var opts = new LensCompilerOptions { AllowSave = true };
			var fx = new LensCompiler(opts).Compile(src);

			for (var idx = 0; idx < 1000; idx ++)
			{
				var res = (double) fx();
				Assert.IsTrue(res >= 0 && res <= 1);
			}
		}

		[Test]
		public void RandTest2()
		{
			var src = "rand 1 1000";
			var opts = new LensCompilerOptions { AllowSave = true };
			var fx = new LensCompiler(opts).Compile(src);

			for (var idx = 0; idx < 1000; idx++)
			{
				var res = (int)fx();
				Assert.IsTrue(res >= 1 && res <= 1000);
			}
		}

		[Test]
		public void RandTest3()
		{
			var src = "rand (new [1; 2; 3; 4; 5])";
			var opts = new LensCompilerOptions { AllowSave = true };
			var fx = new LensCompiler(opts).Compile(src);

			for (var idx = 0; idx < 100; idx++)
			{
				var res = (int)fx();
				Assert.IsTrue(res >= 1 && res <= 5);
			}
		}

		[Test]
		public void ClampTest()
		{
			Test("1.clamp 10 20", 10);
			Test("100.clamp 10 20", 20);

			Test("1.2.clamp 10 20", 10.0);
			Test("1337.1.clamp 10 20", 20.0);
		}

		[Test]
		public void RangeIntTest()
		{
			Test("1.to 5", new[] {1, 2, 3, 4, 5});
			Test("5.to 1", new[] { 5, 4, 3, 2, 1 });
			Test("1.to 100 3", Enumerable.Range(1, 100).Where((x, id) => id % 3 == 0));
			Test("100.to 1 3", Enumerable.Range(1, 100).Reverse().Where((x, id) => id % 3 == 0));
		}

		[Test]
		public void RangeStringTest()
		{
			Test(@"""A"".to ""F""", new[] { "A", "B", "C", "D", "E", "F" });
			Test(@"""F"".to ""A""", new[] { "F", "E", "D", "C", "B", "A" });
			Test(@"""A"".to ""F"" 2", new[] { "A", "C", "E" });
			Test(@"""F"".to ""A"" 2", new[] { "F", "D", "B" });
		}

		[Test]
		public void NullRefTest()
		{
			var src = @"
let tos = x:int -> x.ToString()
tos 5
";
			Test(src, "5");
		}
	}
}
