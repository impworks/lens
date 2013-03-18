using System.Collections.Generic;
using Lens.Parser;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.SyntaxTree.Expressions;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.SyntaxTree.Operators;
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
					new RecordField { Name = "Name", Type = "string" },
					new RecordField { Name = "Age", Type = "int" }
				}
			};

			Test(src, result);
		}

		[Test]
		public void Type()
		{
			var src = @"
type Suit
    Hearts
    Clubs
    Spades
    Diamonds";

			var result = new TypeDefinitionNode
			{
				Name = "Suit",
				Entries =
				{
					new TypeLabel {Name = "Hearts"},
					new TypeLabel {Name = "Clubs"},
					new TypeLabel {Name = "Spades"},
					new TypeLabel {Name = "Diamonds"}
				}
			};

			Test(src, result);
		}

		[Test]
		public void ComplexType()
		{
			var src = @"
type Card
    Ace of Suit
    King of Suit
    Queen of Suit
    Jack of Suit
    ValueCard of Tuple<Suit, int>";

			var result = new TypeDefinitionNode
			{
				Name = "Card",
				Entries =
				{
					new TypeLabel {Name = "Ace", TagType = "Suit"},
					new TypeLabel {Name = "King", TagType = "Suit"},
					new TypeLabel {Name = "Queen", TagType = "Suit"},
					new TypeLabel {Name = "Jack", TagType = "Suit"},
					new TypeLabel {Name = "ValueCard", TagType = "Tuple<Suit,int>"}
				}
			};

			Test(src, result);
		}

		[Test]
		public void ArrayType()
		{
			var src = @"
type ArrayHolder
    Array of int[][]";

			var result = new TypeDefinitionNode
			{
				Name = "ArrayHolder",
				Entries = { new TypeLabel { Name = "Array", TagType = "int[][]" } }
			};

			Test(src, result);
		}

		[Test]
		public void SimpleFunction()
		{
			var src = @"fun negate of int x:int -> -x";
			var result = new FunctionNode
			{
				Name = "negate",
				ReturnTypeSignature = new TypeSignature("int"),
				Arguments =
				{
					new FunctionArgument("x", "int")
				},
				Body =
				{
					Expr.Negate(Expr.Get("x"))
				}
			};

			Test(src, result);
		}

		[Test]
		public void InvocationTest()
		{
			var result = Expr.Invoke(
				Expr.Get("sqrt"),
				Expr.Add(
					Expr.Get("sq1"),
					Expr.Get("sq2")
					)
				);

			Test("sqrt (sq1 + sq2)", result);
		}

		[Test]
		public void ComplexFunction()
		{
			var src = @"fun hypo of double a:int b:int ->
    let sq1 = a * a
    let sq2 = b * b
    sqrt (sq1 + sq2)";

			var result = new FunctionNode
			{
				Name = "hypo",
				ReturnTypeSignature = new TypeSignature("double"),
				Arguments = new List<FunctionArgument>
				{
					new FunctionArgument("a", "int"),
					new FunctionArgument("b", "int")
				},
				Body = 
				{
					Expr.Let(
						"sq1",
						Expr.Mult(
							Expr.Get("a"),
							Expr.Get("a")
						)
					),
					Expr.Let(
						"sq2",
						Expr.Mult(
							Expr.Get("b"),
							Expr.Get("b")
						)
					),
					Expr.Invoke(
						"sqrt",
						Expr.Add(
							Expr.Get("sq1"),
							Expr.Get("sq2")
						)
					)
				}
			};

			Test(src, result);
		}

		[Test]
		public void VariableDeclaration()
		{
			var src = @"var v = 1 + 1";
			var result = Expr.Var("v", Expr.Add(Expr.Int(1), Expr.Int(1)));

			Test(src, result);
		}

		[Test]
		public void ConstantDeclaration()
		{
			var src = @"let v = 1 + 1";
			var result = Expr.Let("v", Expr.Add(Expr.Int(1), Expr.Int(1)));

			Test(src, result);
		}

		[Test]
		public void ArrayDeclaration()
		{
			var src = @"new [1; 2; 1 + 2]";
			var result = Expr.Array(
				Expr.Int(1),
				Expr.Int(2),
				Expr.Add(Expr.Int(1), Expr.Int(2))
			);

			Test(src, result);
		}

		[Test]
		public void TupleDeclaration()
		{
			var src = @"new (1; 1.2; ""hello world""; true)";
			var result = Expr.Tuple(
				Expr.Int(1),
				Expr.Double(1.2),
				Expr.String("hello world"),
				Expr.True()
			);

			Test(src, result);
		}

		[Test]
		public void NewObjectDeclaration()
		{
			var src = @"new SomeObject false 13.37";
			var result = Expr.NewObject(
				"SomeObject",
				Expr.False(),
				Expr.Double(13.37)
			);

			Test(src, result);
		}

		[Test]
		public void DictionaryDeclaration()
		{
			var src = @"new { a => b; c => true }";
			var result = new NewDictionaryNode
			{
				{ Expr.Get("a"), Expr.Get("b")},
				{ Expr.Get("c"), Expr.True()}
			};

			Test(src, result);
		}

		[Test]
		public void ListDeclaration()
		{
			var src = "new <true;true;false>";
			var result = Expr.List(
				Expr.True(),
				Expr.True(),
				Expr.False()
			);

			Test(src, result);
		}

		[Test]
		public void BareLambda()
		{
			var src = "let getFive = -> 5";
			var result = new LetNode("getFive")
			{
				Value = new LambdaNode
				{
					Body = { Expr.Int(5) }
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
				Value = new LambdaNode
				{
					Arguments =
					{
						new FunctionArgument("a", "System.Float"),
						new FunctionArgument("b", "System.Float")
					},
					Body =
					{
						Expr.Div(Expr.Get("a"), Expr.Get("b"))
					}
				}
			};

			Test(src, result);
		}

		[Test]
		public void AssignVariable()
		{
			var src = "a = b";
			var result = Expr.Set("a", Expr.Get("b"));

			Test(src, result);
		}

		[Test]
		public void GetLiteralMember()
		{
			var src = "1.GetHashCode()";
			var result = Expr.Invoke(
				Expr.Int(1),
				"GetHashCode"
			);

			Test(src, result);
		}

		[Test]
		public void GetDynamicMember()
		{
			var src = "a = b.someShit";
			var result = Expr.Set(
				"a",
				Expr.GetMember(
					Expr.Get("b"),
					"someShit"
				)
			);

			Test(src, result);
		}

		[Test]
		public void GetDynamicMember2()
		{
			var src = "a = (1 + 2).someShit";
			var result = Expr.Set(
				"a", 
				Expr.GetMember(
					Expr.Add(Expr.Int(1), Expr.Int(2)),
					"someShit"
				)
			);

			Test(src, result);
		}

		[Test]
		public void GetStaticMember()
		{
			var src = "a = Enumerable<int>::Empty";
			var result = Expr.Set(
				"a",
				Expr.GetMember("Enumerable<int>", "Empty")
			);

			Test(src, result);
		}

		[Test]
		public void SetDynamicMember()
		{
			var src = "a.b.c = false";
			var result = Expr.SetMember(
				Expr.GetMember(Expr.Get("a"), "b"),
				"c",
				Expr.False()
			);

			Test(src, result);
		}

		[Test]
		public void SetDynamicMember2()
		{
			var src = "(1 + 2).someShit = a";
			var result = Expr.SetMember(
				Expr.Add(Expr.Int(1), Expr.Int(2)),
				"someShit",
				Expr.Get("a")
			);

			Test(src, result);
		}

		[Test]
		public void SetStaticMember()
		{
			var src = "Singleton::Instance = null";
			var result = Expr.SetMember("Singleton", "Instance", Expr.Null());

			Test(src, result);
		}

		[Test]
		public void GetIndex()
		{
			var src = "a = b[2**2]";
			var result = Expr.Set(
				"a",
				Expr.GetIndex(
					Expr.Get("b"),
					Expr.Pow(
						Expr.Int(2),
						Expr.Int(2)
					)
				)
			);

			Test(src, result);
		}

		[Test]
		public void SetIndex()
		{
			var src = "a[2**2] = test 1 2 3";
			var result = Expr.SetIndex(
				Expr.Get("a"),
				Expr.Pow(Expr.Int(2), Expr.Int(2)),
				Expr.Invoke(
					"test",
					Expr.Int(1),
					Expr.Int(2),
					Expr.Int(3)
				)
			);

			Test(src, result);
		}

		[Test]
		public void MultilineLambda()
		{
			var src = @"
(a:double) ->
    logger.log a
    a ** 2";

			var result = new LambdaNode
				{
					Arguments = { new FunctionArgument("a", "double") },
					Body = Expr.Block(
						Expr.Invoke(
							Expr.Get("logger"),
							"log",
							Expr.Get("a")
						),
						Expr.Pow(
							Expr.Get("a"),
							Expr.Int(2)
						)
					)
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

			var result = Expr.Invoke(
				"test",
				Expr.True(),
				new LambdaNode
				{
					Arguments = { new FunctionArgument("a", "double") },
					Body = Expr.Block(
						Expr.Invoke(
							Expr.Get("logger"),
							"log",
							Expr.Get("a")
						),
						Expr.Pow(
							Expr.Get("a"),
							Expr.Int(2)
						)
					)
				},
				Expr.False()
			);

			Test(src, result);
		}

		[Test]
		public void ConditionSimple()
		{
			var src = @"
if (true)
    a = 1
    b = 2";
			var result = Expr.If(
				Expr.True(),
				Expr.Block(
					Expr.Set("a", Expr.Int(1)),
					Expr.Set("b", Expr.Int(2))
				)
			);

			Test(src, result);
		}

		[Test]
		public void ConditionFull()
		{
			var src = "if (true) a else b";
			var result = Expr.If(
				Expr.True(),
				Expr.Block(Expr.Get("a")),
				Expr.Block(Expr.Get("b"))
			);

			Test(src, result);
		}

		[Test]
		public void Loop()
		{
			var src = @"while (a > 0) a = a - 1";
			var result = Expr.While(
				Expr.Greater(
					Expr.Get("a"),
					Expr.Int(0)
				),
				Expr.Block(
					Expr.Set(
						"a",
						Expr.Sub(
							Expr.Get("a"),
							Expr.Int(1)
						)
					)
				)
			);

			Test(src, result);
		}

		[Test]
		public void SingleCatch()
		{
			var src = @"
try
    1 / 0
catch (DivisionByZeroException ex)
    log ex
";

			var result = Expr.Try(
				Expr.Block(
					Expr.Div(Expr.Int(1), Expr.Int(0))
				),
				Expr.Catch(
					"DivisionByZeroException",
					"ex",
					Expr.Block(
						Expr.Invoke("log", Expr.Get("ex"))
					)
				)
			);

			Test(src, result);
		}

		[Test]
		public void MultipleCatch()
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

			var result = Expr.Try(
				Expr.Block(
					Expr.Div(Expr.Int(1), Expr.Int(0))
				),
				Expr.Catch(
					"DivisionByZeroException",
					"ex",
					Expr.Block(
						Expr.Invoke("log", Expr.Get("ex"))
					)
				),
				Expr.CatchAll(
					Expr.Invoke("doStuff", Expr.Unit()),
					Expr.Invoke("log", Expr.String("whoopsie"))
				)
			);

			Test(src, result);
		}

		[Test]
		public void Typeof()
		{
			var src = "let a = typeof int";
			var result = Expr.Let("a", Expr.Typeof("int"));
			Test(src, result);
		}

		[Test]
		public void DefaultOf()
		{
			var src = "let b = default System.Collections.Generic.List<int>";
			var result = Expr.Let("b", Expr.Default("System.Collections.Generic.List<int>"));
			Test(src, result);
		}

		[Test]
		public void Not()
		{
			var src = "not a && b";
			var result = Expr.And(
				Expr.Not(Expr.Get("a")),
				Expr.Get("b")
			);

			Test(src, result);
		}

		[Test]
		public void Cast()
		{
			var src = "let a = (b as List<int>).Count";
			var result = Expr.Let(
				"a",
				Expr.GetMember(
					Expr.Cast(
						Expr.Get("b"),
						"List<int>"
					),
					"Count"
				)
			);

			Test(src, result);
		}

		[Test]
		public void IsOperator()
		{
			var src = "1 is double";
			var result = Expr.IsType(Expr.Int(1), new TypeSignature("double"));
			Test(src, result);
		}

		[Test]
		public void ManyParameters()
		{
			var src = @"test 1337 true ""hello"" (new(13.37; new [1; 2]))";
			var result = Expr.Invoke(
				"test",
				Expr.Int(1337),
				Expr.True(),
				Expr.String("hello"),
				Expr.Tuple(
					Expr.Double(13.37),
					Expr.Array(
						Expr.Int(1),
						Expr.Int(2)
					)
				)
			);

			Test(src, result);
		}

		[Test]
		public void OperatorPriority1()
		{
			var src = "test a b > 10";
			var result = Expr.Greater(
				Expr.Invoke(
					"test",
					Expr.Get("a"),
					Expr.Get("b")
				),
				Expr.Int(10)
			);

			Test(src, result);
		}

		[Test]
		public void OperatorPriority2()
		{
			var src = "1 + 2 - 3 * 4 / 5 % 6 ** 7";
			var result = Expr.Sub(
				Expr.Add(
					Expr.Int(1),
					Expr.Int(2)
				),
				Expr.Remainder(
					Expr.Div(
						Expr.Mult(
							Expr.Int(3),
							Expr.Int(4)
						),
						Expr.Int(5)
					),
					Expr.Pow(
						Expr.Int(6),
						Expr.Int(7)
					)
				)
			);

			Test(src, result);
		}

		[Test]
		public void TrailingWhitespace()
		{
			var src = "1 ";
			var result = Expr.Int(1);
			Test(src, result);
		}

		[Test]
		public void Unit()
		{
			var src = "()";
			var result = Expr.Unit();
			Test(src, result);
		}

		[Test]
		public void DefinitionAndInvocation()
		{
			var src = @"fun test of int -> 10
test ()";
			var definition = new FunctionNode
			{
				Name = "test",
				ReturnTypeSignature = new TypeSignature("int"),
				Body = Expr.Block(Expr.Int(10))
			};
			var invocation = Expr.Invoke("test", Expr.Unit());

			Test(src, definition, invocation);
		}

		[Test]
		public void ThrowExpression()
		{
			var src = "throw new NotImplementedException ()";
			var result = Expr.Throw("NotImplementedException");

			Test(src, result);
		}

		[Test]
		public void Rethrow()
		{
			var src = @"
try
	1 / 0
catch DivisionByZeroException
	throw";

			var result = Expr.Try(
				Expr.Block(
					Expr.Div(Expr.Int(1), Expr.Int(0))
				),
				Expr.Catch(
					"DivisionByZeroException",
					Expr.Block(Expr.Throw())
				)
			);

			Test(src, result);
		}

		[Test]
		public void LinqCall()
		{
			var src = @"new [1, 2]. Where ((x:int) -> x > 1)";
			var result = Expr.Invoke(
				Expr.Array(Expr.Int(1), Expr.Int(2)),
				"Where",
				new LambdaNode
					{
						Arguments = new List<FunctionArgument> { new FunctionArgument("x", "int") },
						Body = Expr.Block(
							Expr.Greater(
								Expr.Get("x"),
								Expr.Int(1)
							)
						)
					}
				);

			Test(src, result);
		}

		[Test]
		public void TypeHints1()
		{
			var src = "Enumerable::Empty<int> ()";
			var result = Expr.Invoke(
				Expr.GetMember("Enumerable", "Empty", "int"),
				Expr.Unit()
			);

			Test(src, result);
		}

		[Test]
		public void TypeHints2()
		{
			var src = @"expr.CallStuff<int, TestType, _> 1 ""hello""";
			var result = Expr.Invoke(
				Expr.GetMember(
					Expr.Get("expr"),
					"CallStuff",
					"int", "TestType", "_"
				),
				Expr.Int(1),
				Expr.String("hello")
			);

			Test(src, result);
		}

		[Test]
		public void TypeHint3()
		{
			var src = @"let a = SomeType::Method<int, _, System.Uri>";
			var result = Expr.Let(
				"a",
				Expr.GetMember(
					"SomeType",
					"Method",
					"int", "_", "System.Uri"
				)
			);

			Test(src, result);
		}

		[Test]
		public void TypeHint4()
		{
			var src = @"let a = b.Method<int, _, System.Uri>";
			var result = Expr.Let(
				"a",
				Expr.GetMember(
					Expr.Get("b"),
					"Method",
					"int", "_", "System.Uri"
				)
			);

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
