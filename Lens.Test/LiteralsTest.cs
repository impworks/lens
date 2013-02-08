using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class LiteralsTest
	{
		[Test]
		public void IntTest()
		{
			Test("1337", 1337);
		}

		[Test]
		public void StringTest()
		{
			Test(@"""testy""", "testy");
		}

		[Test]
		public void BoolTest()
		{
			Test("true", true);
		}

		private void Test(string src, object expected)
		{
			var lc = new LensCompiler();
			var received = lc.Run(src);
			Assert.AreEqual(expected, received);
		}
	}
}
