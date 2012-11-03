using System.Collections.Generic;
using Lens.Parser;
using Lens.SyntaxTree.SyntaxTree;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.SyntaxTree.Expressions;
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
            Test("using System", new UsingNode { Namespace = "System" });
        }

        [Test]
        public void MultiUsing()
        {
            Test("using Lens.Parser", new UsingNode { Namespace = "Lens.Parser" });
        }

        [Test]
        public void Record()
        {
            var src = @"record Student
    Name:string
    Age:int";

            var result = new RecordDefinitionNode
            {
                Name = "Student",
                Entries =
                {
                    new RecordEntry { Name = "Name", Type = new TypeSignature("string") },
                    new RecordEntry { Name = "Age", Type = new TypeSignature("int") }
                }
            };

            Test(src, result);
        }

        [Test]
        public void Type()
        {
            var src = @"type Suit
    | Hearts
    | Clubs
    | Spades
    | Diamonds";

            var result = new TypeDefinitionNode
            {
                Name = "Suit",
                Entries =
                {
                    new TypeEntry {Name = "Hearts"},
                    new TypeEntry {Name = "Clubs"},
                    new TypeEntry {Name = "Spades"},
                    new TypeEntry {Name = "Diamonds"}
                }
            };

            Test(src, result);
        }

        [Test]
        public void ComplexType()
        {
            var src = @"type Card
    | Ace of Suit
    | King of Suit
    | Queen of Suit
    | Jack of Suit
    | ValueCard of Tuple<Suit, int>";

            var result = new TypeDefinitionNode
            {
                Name = "Card",
                Entries =
                {
                    new TypeEntry {Name = "Ace", TagType = new TypeSignature("Suit")},
                    new TypeEntry {Name = "King", TagType = new TypeSignature("Suit")},
                    new TypeEntry {Name = "Queen", TagType = new TypeSignature("Suit")},
                    new TypeEntry {Name = "Jack", TagType = new TypeSignature("Suit")},
                    new TypeEntry {Name = "ValueCard", TagType = new TypeSignature("Tuple<Suit,int>")}
                }
            };

            Test(src, result);
        }

        [Test]
        public void ArrayType()
        {
            var src = @"type ArrayHolder
    | Array of int[][]";

            var result = new TypeDefinitionNode
            {
                Name = "ArrayHolder",
                Entries = { new TypeEntry { Name = "Array", TagType = new TypeSignature("int[][]") } }
            };

            Test(src, result);
        }

        [Test]
        public void SimpleFunction()
        {
            var src = @"fun negate x:int -> -x";
            var result = new NamedFunctionNode
            {
                Name = "negate",
                Arguments = new Dictionary<string, FunctionArgument>
                {
                    {"x", new FunctionArgument {Name = "x", Type = new TypeSignature("int")}}
                },
                Body = new CodeBlockNode
                {
                    Statements = { new NegationOperatorNode {Operand = new GetIdentifierNode {Identifier = "x"}} }
                }
            };

            Test(src, result);
        }

        [Test]
        public void ComplexFunction()
        {
            var src = @"fun hypo a:int b:int ->
    let sq1 = a * a
    let sq2 = b * b
    sqrt (sq1 + sq2)";

            var result = new NamedFunctionNode
            {
                Name = "hypo",
                Arguments = new Dictionary<string, FunctionArgument>
                {
                    {"a", new FunctionArgument {Name = "a", Type = new TypeSignature("int")}},
                    {"b", new FunctionArgument {Name = "b", Type = new TypeSignature("int")}}
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

            Test(src, result);
        }

        [Test]
        public void VariableDeclaration()
        {
            var src = @"var v = 1 + 1";
            var result = new VarNode
            {
                Name = "v",
                Value = new AddOperatorNode
                {
                    LeftOperand = new IntNode(1),
                    RightOperand = new IntNode(1)
                }
            };

            Test(src, result);
        }

        [Test]
        public void ConstantDeclaration()
        {
            var src = @"let v = 1 + 1";
            var result = new LetNode
            {
                Name = "v",
                Value = new AddOperatorNode
                {
                    LeftOperand = new IntNode(1),
                    RightOperand = new IntNode(1)
                }
            };

            Test(src, result);
        }

        [Test]
        public void ArrayDeclaration()
        {
            var src = @"new [1; 2; 1 + 2]";
	        var result = new NewArrayNode
		    {
			    Expressions =
				{
					new IntNode(1),
					new IntNode(2),
					new AddOperatorNode
					{
						LeftOperand = new IntNode(1),
						RightOperand = new IntNode(2)
					}
				}
		    };

            Test(src, result);
        }

        private static void Test(string source, params NodeBase[] expected)
        {
            var treeBuilder = new TreeBuilder();
            var result = treeBuilder.Parse(source);
            Assert.AreEqual(expected, result);
        }
    }
}
