using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lens.SyntaxTree.Compiler;
using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class ExtensionMethodResolverTest
	{
		[Test]
		public void TestEnumerable1()
		{
			var found = typeof (IEnumerable<int>).FindExtensionMethod("Where", new[] {typeof (Func<int, bool>)});
			var bucket = typeof (Enumerable).GetMethods().Where(m => m.Name == "Where").ToArray();
			Assert.Contains(found, bucket);
		}

		[Test]
		public void TestEnumerable2()
		{
			var found = typeof(string[]).FindExtensionMethod("Select", new[] { typeof(Func<string, int>) });
			var bucket = typeof(Enumerable).GetMethods().Where(m => m.Name == "Select").ToArray();
			Assert.Contains(found, bucket);
		}

		[Test]
		public void TestEnumerable3()
		{
			var found = typeof (int[]).FindExtensionMethod("Max", Type.EmptyTypes);
			var bucket = typeof (Enumerable).GetMethods().Where(m => m.Name == "Max").ToArray();
			Assert.Contains(found, bucket);
		}
	}
}
