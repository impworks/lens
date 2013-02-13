using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class Snippets
	{
		[Test]
		public void SaveAndLoadLocal()
		{
			var src1 = @"
var a = 1
a";

			var src2 = @"
var a = 1
var b = new [a; 2]
b";

			Test(src1, 1);
			Test(src2, new [] { 1, 2 });
		}

		private void Test(string src, object value)
		{
			Assert.AreEqual(Compile(src), value);
		}

		private object Compile(string src)
		{
			return new LensCompiler().Run(src);
		}
	}
}
