using System;
using System.Collections.Generic;
using System.Linq;
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
		public void DoubleTest()
		{
			Test("1.337", 1.337);
		}

		[Test]
		public void BoolTest()
		{
			Test("true", true);
		}

		[Test]
		public void UnitTest()
		{
			Test("()", null);
		}

		[Test]
		public void ArrayTest()
		{
			var result = Compile("new [1; 2; 3]");
			Assert.IsInstanceOf<int[]>(result);
			Assert.True((result as IEnumerable<int>).SequenceEqual(new [] { 1, 2, 3 }));
		}

		private void Test(string src, object expected)
		{
			Assert.AreEqual(expected, Compile(src));
		}

		private object Compile(string src)
		{
			return new LensCompiler().Run(src);
		}
	}
}
