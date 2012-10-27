using System.Linq;
using Lens.Parser;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.SyntaxTree.Operators;
using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class ParserTest
	{
		[Test]
		public void Sum()
		{
			const string source = @"2+2";
			var parser = new TreeBuilder();
			var result = parser.Parse(source);
			var expected = new AddOperatorNode
				{
					LeftOperand = new IntNode {Value = 2},
					RightOperand = new IntNode {Value = 2}
				};
			Assert.AreEqual(expected, result.Single());
		}
	}
}
