using System;
using Lens.SyntaxTree;
using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class StdlibTest
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
5.times ((x:int) -> r = r + x.ToString())
r
";
			Test(src, "01234");
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

		private void Test(string src, object value)
		{
			Assert.AreEqual(value, Compile(src));
		}

		private object Compile(string src)
		{
			var opts = new LensCompilerOptions { AllowSave = true };
			return new LensCompiler(opts).Run(src);
		}
	}
}
