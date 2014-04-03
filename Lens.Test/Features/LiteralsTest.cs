using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Lens.Test.Features
{
	[TestFixture]
	internal class LiteralsTest : TestBase
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
			Assert.True((result as IEnumerable<int>).SequenceEqual(new[] { 1, 2, 3 }));
		}

		[Test]
		public void TupleTest()
		{
			var result = Compile(@"new (1; true; ""hello"")");
			var tuple = result as Tuple<int, bool, string>;
			Assert.AreEqual(new Tuple<int, bool, string>(1, true, "hello"), tuple);
		}

		[Test]
		public void ListTest()
		{
			var result = Compile(@"new [[1; 42; 1337]]");
			var list = result as List<int>;
			Assert.AreEqual(new List<int> { 1, 42, 1337 }, list);
		}

		[Test]
		public void DictTest()
		{
			var result = Compile(@"new { 1 => true; 2 => false; 42 => true }");
			var list = result as Dictionary<int, bool>;
			var expected = new Dictionary<int, bool> {{1, true}, {2, false}, {42, true}};
			Assert.AreEqual(expected, list);
		}
	}
}
