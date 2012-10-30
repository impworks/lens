using System.Collections.Generic;
using Lens.Parser;
using Lens.SyntaxTree.SyntaxTree;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.SyntaxTree.Expressions;
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
			Test("using System", new UsingNode {Namespace = "System"});
		}

		[Test]
		public void MultiUsing()
		{
			Test("using Lens::Parser", new UsingNode {Namespace = "Lens.Parser"});
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
		public void Type()
		{
			var result = new TypeDefinitionNode {Name = "Suit"};
			result.Entries.AddRange(new[]
				{
					new TypeEntry {Name = "Hearts"},
					new TypeEntry {Name = "Clubs"},
					new TypeEntry {Name = "Spades"},
					new TypeEntry {Name = "Diamonds"}
				});

			Test(
				@"type Suit
    | Hearts
    | Clubs
    | Spades
    | Diamonds",
				result);
		}

		[Test]
		public void ComplexType()
		{
			var result = new TypeDefinitionNode {Name = "Card"};
			result.Entries.AddRange(new[]
				{
					new TypeEntry {Name = "Ace", TagType = new TypeSignature("Suit")},
					new TypeEntry {Name = "King", TagType = new TypeSignature("Suit")},
					new TypeEntry {Name = "Queen", TagType = new TypeSignature("Suit")},
					new TypeEntry {Name = "Jack", TagType = new TypeSignature("Suit")},
					new TypeEntry {Name = "ValueCard", TagType = new TypeSignature("Tuple<Suit,int>")}
				});

			Test(
				@"type Card
    | Ace of Suit
    | King of Suit
    | Queen of Suit
    | Jack of Suit
    | ValueCard of Tuple<Suit, int>",
				result);
		}

		[Test]
		public void ArrayType()
		{
			var result = new TypeDefinitionNode {Name = "ArrayHolder"};
			result.Entries.Add(new TypeEntry {Name = "Array", TagType = new TypeSignature("int[][]")});

			Test(
				@"type ArrayHolder
    | Array of int[][]",
				result);
		}

		[Test]
		public void SimpleFunction()
		{
			var result = new NamedFunctionNode
				{
					Name = "negate",
					Arguments = new Dictionary<string, FunctionArgument>
						{
							{"x", new FunctionArgument {Name = "x", Type = "int"}}
						},
					Body = new CodeBlockNode
						{
							Statements = {new NegationOperator {Operand = new GetIdentifierNode {Identifier = "x"}}}
						}
				};

			Test("fun negate x:int -> -x", result);
		}

		[Test]
		public void ComplexFunction()
		{
			var result = new NamedFunctionNode
				{
					Name = "hypo",
					Arguments = new Dictionary<string, FunctionArgument>
						{
							{"a", new FunctionArgument {Name = "a", Type = "int"}},
							{"b", new FunctionArgument {Name = "b", Type = "int"}}
						},
					Body = new CodeBlockNode
						{
							Statements =
								{
									new LetNode
										{
											Name = "sq1",
											Value = new MultiplyOperatorNode
												{
													LeftOperand = new GetIdentifierNode {Identifier = "a"},
													RightOperand = new GetIdentifierNode {Identifier = "a"}
												}
										},
									new LetNode
										{
											Name = "sq2",
											Value = new MultiplyOperatorNode
												{
													LeftOperand = new GetIdentifierNode {Identifier = "b"},
													RightOperand = new GetIdentifierNode {Identifier = "b"}
												}
										},
									new InvocationNode
										{
											MethodName = "sqrt",
											Arguments =
												{
													new AddOperatorNode
														{
															LeftOperand = new GetIdentifierNode {Identifier = "sq1"},
															RightOperand = new GetIdentifierNode {Identifier = "sq2"}
														}
												}
										}
								}
						}
				};

			Test(
				@"fun hypo a:int b:int ->
    let sq1 = a * a
    let sq2 = b * b
    sqrt (sq1 + sq2)",
				result);
		}

		private static void Test(string source, params NodeBase[] expected)
		{
			var treeBuilder = new TreeBuilder();
			var result = treeBuilder.Parse(source);
			Assert.AreEqual(expected, result);
		}
	}
}
