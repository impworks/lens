using Lens.SyntaxTree;
using NUnit.Framework;

namespace Lens.Test
{
	class AttributeTest : TestBase
	{
		[Test]
		public void AttributeDefinition()
		{
			var script = @"
@AttributeName
fun foo (x:int) -> 10";
			var expected = Expr.Fun(
				"foo",
				null,
				false,
				new[] { Expr.Arg("x", "int") },
				new [] { Expr.Attribute("AttributeName") },
				Expr.Int(10));
			TestParser(script, expected);
		}
	}
}
