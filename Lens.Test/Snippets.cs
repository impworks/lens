using Lens.SyntaxTree;
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

		[Test]
		public void InvokeStatic()
		{
			Test(@"string::Compare ""a"" ""b""", -1);
			Test(@"Console::WriteLine ""a""", null);
			Assert.Throws<LensCompilerException>(() => Compile(@"string::Compare ""a"" "));
		}

		[Test]
		public void InvokeDynamic()
		{
			Test(@"(1).GetHashCode ()", 1);
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
