using System;
using Lens.Parser;
using Lens.SyntaxTree.SyntaxTree;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.SyntaxTree.Operators;
using Lens.SyntaxTree.Utils;
using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class ParserTest
	{
		[Test]
		public void Using()
		{
			// TODO: Test("using System", new UsingNode("System"))
			throw new Exception("Using node not supported, test failed");
		}

		[Test]
		public void Record()
		{
			var result = new RecordDefinitionNode {Name = "Student"};
			result.Entries.AddRange(new[]
				{
					new RecordEntry {Name = "Name", Type = new TypeSignature("string")},
					new RecordEntry {Name = "Age", Type = new TypeSignature("int")}
				});

			Test(
				@"record Student
    Name:string
    Age:int",
				result);
		}

		[Test]
		public void Sum()
		{
			Test(
				@"2+2",
				new AddOperatorNode
					{
						LeftOperand = new IntNode {Value = 2},
						RightOperand = new IntNode {Value = 2}
					});
		}

		private static void Test(string source, params NodeBase[] expected)
		{
			var treeBuilder = new TreeBuilder();
			var result = treeBuilder.Parse(source);
			Assert.AreEqual(expected, result);
		}
	}
}
