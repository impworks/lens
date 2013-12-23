using Lens.Compiler;
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
def foo() = 10";
			var result = Parse(script);
			var expected = Expr.Fun(
				"x",
				null,
				false,
				new FunctionArgument[0],
				new [] { Expr.Attribute("AttributeName") },
				Expr.Int(10));
			Assert.AreEqual(expected, result);
		}
	}
}
