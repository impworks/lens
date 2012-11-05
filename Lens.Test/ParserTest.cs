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
            var src = @"
record Student
    Name:string
    Age:int";

            var result = new RecordDefinitionNode
            {
                Name = "Student",
                Entries =
                {
                    new RecordEntry { Name = "Name", Type = "string" },
                    new RecordEntry { Name = "Age", Type = "int" }
                }
            };

            Test(src, result);
        }

        [Test]
        public void Type()
        {
            var src = @"
type Suit
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
            var src = @"
type Card
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
                    new TypeEntry {Name = "Ace", TagType = "Suit"},
                    new TypeEntry {Name = "King", TagType = "Suit"},
                    new TypeEntry {Name = "Queen", TagType = "Suit"},
                    new TypeEntry {Name = "Jack", TagType = "Suit"},
                    new TypeEntry {Name = "ValueCard", TagType = "Tuple<Suit,int>"}
                }
            };

            Test(src, result);
        }

        [Test]
        public void ArrayType()
        {
            var src = @"
type ArrayHolder
    | Array of int[][]";

            var result = new TypeDefinitionNode
            {
                Name = "ArrayHolder",
                Entries = { new TypeEntry { Name = "Array", TagType = "int[][]" } }
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
                Arguments =
                {
                    {"x", new FunctionArgument("x", "int")}
                },
                Body =
                {
                    new NegationOperatorNode { Operand = new GetIdentifierNode("x") }
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
                    {"a", new FunctionArgument("a", "int")},
                    {"b", new FunctionArgument("b", "int")}
                },
                Body = 
                {
                    new LetNode
                    {
                        Name = "sq1",
                        Value = new MultiplyOperatorNode
                        {
                            LeftOperand = new GetIdentifierNode("a"),
                            RightOperand = new GetIdentifierNode("a")
                        }
                    },
                    new LetNode
                    {
                        Name = "sq2",
                        Value = new MultiplyOperatorNode
                        {
                            LeftOperand = new GetIdentifierNode("b"),
                            RightOperand = new GetIdentifierNode("b")
                        }
                    },
                    new InvocationNode
                    {
                        MethodName = "sqrt",
                        Arguments =
                        {
                            new AddOperatorNode
                            {
                                LeftOperand = new GetIdentifierNode("sq1"),
                                RightOperand = new GetIdentifierNode("sq2")
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
            var result = new VarNode("v")
            {
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
            var result = new LetNode("v")
            {
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

		[Test]
		public void TupleDeclaration()
		{
			var src = @"new (1; 1.2; ""hello world""; true)";
			var result = new NewTupleNode
			{
				Expressions =
				{
					new IntNode(1),
					new DoubleNode(1.2),
					new StringNode("hello world"),
					new BooleanNode(true)
				}
			};

			Test(src, result);
		}

		[Test]
		public void NewObjectDeclaration()
		{
			var src = @"new SomeObject false 13.37";
			var result = new NewObjectNode("SomeObject")
			{
				Arguments =
				{
					new BooleanNode(),
					new DoubleNode(13.37)
				}
			};

			Test(src, result);
		}

		[Test]
		public void BareLambda()
		{
			var src = "let getFive = -> 5";
			var result = new LetNode("getFive")
			{
				Value = new FunctionNode
				{
					Body = { new IntNode(5) }
				}
			};

			Test(src, result);
		}

		[Test]
		public void ParametricLambda()
		{
			var src = "let div = (a:System.Float b:System.Float) -> a / b";
			var result = new LetNode("div")
			{
				Value = new FunctionNode
				{
					Arguments =
					{
						{"a", new FunctionArgument("a", "System.Float") },
						{"b", new FunctionArgument("b", "System.Float") }
					},
					Body =
					{
						new DivideOperatorNode
						{
							LeftOperand = new GetIdentifierNode("a"),
							RightOperand = new GetIdentifierNode("b"),
						}
					}
				}
			};

			Test(src, result);
		}

		[Test]
		public void AssignVariable()
		{
			var src = "a = b";
			var result = new SetIdentifierNode
			{
				Identifier = "a",
				Value = new GetIdentifierNode("b")
			};

			Test(src, result);
		}

		[Test]
		public void GetDynamicMember()
		{
			var src = "a = b.someShit";
			var result = new SetIdentifierNode
			{
				Identifier = "a",
				Value = new GetMemberNode
				{
					MemberName = "someShit",
					Expression = new GetIdentifierNode("b")
				}
			};

			Test(src, result);
		}

		[Test]
		public void GetDynamicMember2()
		{
			var src = "a = (1 + 2).someShit";
			var result = new SetIdentifierNode
			{
				Identifier = "a",
				Value = new GetMemberNode
				{
					MemberName = "someShit",
					Expression = new AddOperatorNode
					{
						LeftOperand = new IntNode(1),
						RightOperand = new IntNode(2)
					}
				}
			};

			Test(src, result);
		}

		[Test]
		public void GetStaticMember()
		{
			var src = "a = Enumerable<int>::Empty";
			var result = new SetIdentifierNode
			{
				Identifier = "a",
				Value = new GetMemberNode
				{
					MemberName = "Empty",
					StaticType = "Enumerable<int>"
				}
			};

			Test(src, result);
		}

		[Test]
		public void SetDynamicMember()
		{
			var src = "a.b.c = false";
			var result = new SetMemberNode
			{
				MemberName = "c",
				Value = new BooleanNode(),
				Expression = new GetMemberNode
				{
					MemberName = "b",
					Expression = new GetIdentifierNode("a")
				}
			};

			Test(src, result);
		}

		[Test]
		public void SetStaticMember()
		{
			var src = "Singleton::Instance = null";
			var result = new SetMemberNode
			{
				MemberName = "Instance",
				StaticType = "Singleton",
				Value = new NullNode()
			};

			Test(src, result);
		}

		[Test]
		public void GetIndex()
		{
			var src = "a = b[2**2]";
			var result = new SetIdentifierNode("a")
			{
				Value = new GetIndexNode
				{
					Expression = new GetIdentifierNode("b"),
					Index = new PowOperatorNode
					{
						LeftOperand = new IntNode(2),
						RightOperand = new IntNode(2)
					}
				}
			};

			Test(src, result);
		}

		[Test]
		public void SetIndex()
		{
			var src = "a[2**2] = test 1 2 3";
			var result = new SetIndexNode
			{
				Expression = new GetIdentifierNode("a"),
				Index = new PowOperatorNode
				{
					LeftOperand = new IntNode(2),
					RightOperand = new IntNode(2)
				},
				Value = new InvocationNode
				{
					MethodName = "test",
					Arguments =
					{
						new IntNode(1),
						new IntNode(2),
						new IntNode(3),
					}
				}
			};

			Test(src, result);
		}

		[Test]
		public void MultilineInvocation()
		{
			var src = @"
test
    <| true
    <| (a:double) ->
        logger.log a
        a ** 2
    <| false";

			var result = new InvocationNode
			{
				MethodName = "test",
				Arguments =
				{
					new BooleanNode(true),
					new FunctionNode
					{
						Arguments = {{"a", new FunctionArgument("a", "double")}},
						Body =
						{
							new InvocationNode
							{
								Expression = new GetIdentifierNode("logger"),
								MethodName = "log",
								Arguments = {new GetIdentifierNode("a")}
							},
							new PowOperatorNode
							{
								LeftOperand = new GetIdentifierNode("a"),
								RightOperand = new IntNode(2)
							}
						}
					},
					new BooleanNode()
				}
			};

			Test(src, result);
		}

		[Test]
		public void ConditionSimple()
		{
			var src = @"
if (true)
    a = 1
    b = 2";
			var result = new ConditionNode
			{
				Condition = new BooleanNode(true),
				TrueAction =
				{
					new SetIdentifierNode("a") { Value = new IntNode(1) },
					new SetIdentifierNode("b") { Value = new IntNode(2) }
				}
			};

			Test(src, result);
		}

		[Test]
		public void ConditionFull()
		{
			var src = "if (true) a else b";
			var result = new ConditionNode
			{
				Condition = new BooleanNode(true),
				TrueAction = {new GetIdentifierNode("a")},
				FalseAction = new CodeBlockNode {new GetIdentifierNode("b")}
			};

			Test(src, result);
		}

		[Test]
		public void Loop()
		{
			var src = @"while (a > 0) a = a - 1";
			var result = new LoopNode
			{
				Condition = new ComparisonOperatorNode(ComparisonOperatorKind.Greater)
				{
					LeftOperand = new GetIdentifierNode("a"),
					RightOperand = new IntNode()
				},
				Body =
				{
					new SetIdentifierNode("a")
					{
						Value = new SubtractOperatorNode
						{
							LeftOperand = new GetIdentifierNode("a"),
							RightOperand = new IntNode(1)
						}
					}
				}
			};

			Test(src, result);
		}

		[Test]
		public void TryCatch()
		{
			var src = @"
try
    1 / 0
catch (DivisionByZeroException ex)
    log ex
catch
    doStuff ()
    log ""whoopsie""
";

			var result = new TryNode
			{
				Code =
				{
					new DivideOperatorNode
					{
						LeftOperand = new IntNode(1),
						RightOperand = new IntNode()
					}
				},
				CatchClauses =
				{
					new CatchNode
					{
						ExceptionType = "DivisionByZeroException",
						ExceptionVariable = "ex",
						Code =
						{
							new InvocationNode
							{
								MethodName = "log",
								Arguments = {new GetIdentifierNode("ex")}
							}
						}
					},
					new CatchNode
					{
						Code =
						{
							new InvocationNode
							{
								MethodName = "doStuff",
								Arguments = {new UnitNode()}
							},
							new InvocationNode
							{
								MethodName = "log",
								Arguments = {new StringNode("whoopsie")}
							}
						}
					}
				}
			};

			Test(src, result);
		}

		[Test]
		public void Typeof()
		{
			var src = "let a = typeof(int)";
			var result = new LetNode("a")
			{
				Value = new TypeofOperatorNode("int")
			};

			Test(src, result);
		}

		[Test]
		public void DefaultOf()
		{
			var src = "let b = default (System.Collections.Generic.List<int>)";
			var result = new LetNode("b")
			{
				Value = new DefaultOperatorNode("System.Collections.Generic.List<int>")
			};

			Test(src, result);
		}

		[Test]
		public void Not()
		{
			var src = "not a && b";
			var result = new BooleanOperatorNode(BooleanOperatorKind.And)
			{
				LeftOperand = new InversionOperatorNode
				{
					Operand = new GetIdentifierNode("a")
				},
				RightOperand = new GetIdentifierNode("b")
			};

			Test(src, result);
		}

		[Test]
		public void Cast()
		{
			var src = "let a = (b as List<int>).Count";
			var result = new LetNode("a")
			{
				Value = new GetMemberNode
				{
					MemberName = "Count",
					Expression = new CastOperatorNode
					{
						Expression = new GetIdentifierNode("b"),
						Type = "List<int>"
					}
				}
			};

			Test(src, result);
		}

		[Test]
		public void ManyParameters()
		{
			var src = @"test 1337 true ""hello"" new(13.37; new [1; 2])";
			var result = new InvocationNode
			{
				MethodName = "test",
				Arguments =
				{
					new IntNode(1337),
					new BooleanNode(true),
					new StringNode("hello"),
					new NewTupleNode
					{
						Expressions =
						{
							new DoubleNode(13.37),
							new NewArrayNode
							{
								Expressions =
								{
									new IntNode(1),
									new IntNode(2)
								}
							}
						}
					}
				}
			};

			Test(src, result);
		}

		[Test]
		public void OperatorPriority1()
		{
			var src = "test a b > 10";
			var result = new ComparisonOperatorNode(ComparisonOperatorKind.Greater)
			{
				LeftOperand = new InvocationNode
				{
					MethodName = "test",
					Arguments =
					{
						new GetIdentifierNode("a"),
						new GetIdentifierNode("b"),
					}
				},
				RightOperand = new IntNode(10)
			};

			Test(src, result);
		}

		[Test]
		public void OperatorPriority2()
		{
			var src = "1 + 2 - 3 * 4 / 5 % 6 ** 7";
			var result = new AddOperatorNode
			{
				LeftOperand = new IntNode(1),
				RightOperand = new SubtractOperatorNode
				{
					LeftOperand = new IntNode(2),
					RightOperand = new MultiplyOperatorNode
					{
						LeftOperand = new IntNode(3),
						RightOperand = new DivideOperatorNode
						{
							LeftOperand = new IntNode(4),
							RightOperand = new RemainderOperatorNode
							{
								LeftOperand = new IntNode(5),
								RightOperand = new PowOperatorNode
								{
									LeftOperand = new IntNode(6),
									RightOperand = new IntNode(7)
								}
							}
						}
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
