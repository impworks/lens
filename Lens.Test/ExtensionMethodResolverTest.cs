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
		public void TestEnumerable()
		{
			var m = typeof (IEnumerable<int>).FindExtensionMethod("Where", new[] {typeof (Func<int, bool>)});
			Assert.IsNotNull(m);
		}
	}
}
