using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;
using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class Snippets
	{
		[Test]
		public void SaveAndLoadLocal()
		{
			var src1 = @"
var a = 1
a";

			var src2 = @"
var a = 1
var b = new [a; 2]
b";

			Test(src1, 1);
			Test(src2, new [] { 1, 2 });
		}

		[Test]
		public void InvokeStatic()
		{
			Test(@"string::Compare ""a"" ""b""", -1);
			Test(@"Console::WriteLine ""a""", null);
			Assert.Throws<LensCompilerException>(() => Compile(@"string::Compare ""a"" "));
		}

		[Test]
		public void InvokeDynamic()
		{
			Test(@"1.GetHashCode ()", 1);
		}

		[Test]
		public void DeclareAndInvoke()
		{
			var src = @"
fun test of int -> 10
test ()";
			Test(src, 10);
		}

		[Test]
		public void ArrayIndexSetter()
		{
			var src = @"
var arr = new [1; 2; 3]
arr[1] = 10
arr[1] + arr[0]";
			Test(src, 11);
		}

		[Test]
		public void ListIndexSetter()
		{
			var src = @"
var list = new <1; 2; 3>
list[1] = 10
list[1] + list[0]";
			Test(src, 11);
		}

		[Test]
		public void DictIndexSetter()
		{
			var src = @"
var dict = new { ""a"" => 1; ""b"" => 2 }
dict[""a""] = 2
dict[""a""] + dict[""b""]
";
			Test(src, 4);
		}

		[Test]
		public void Loop()
		{
			var src = @"
var a = 1
var idx = 0
while(idx < 5)
    a = a * 2
    idx = idx + 1
a";

			Test(src, 32);
		}

		[Test]
		public void LoopResult()
		{
			var src = @"
var a = 1
var res = while (a < 10)
    a = a * 2
    a
res";
			Test(src, 16);
		}
	
		[Test]
		public void ThrowNew()
		{
			var src = "throw new NotImplementedException ()";
			Assert.Throws<NotImplementedException>(() => Compile(src));
		}

		[Test]
		public void ThrowExisting()
		{
			var src = @"
var ex = new NotImplementedException ()
throw ex";

			Assert.Throws<NotImplementedException>(() => Compile(src));
		}

		[Test]
		public void GetProperty()
		{
			var src = @"(new (1; 2)).Item1";
			Test(src, 1);
		}

		[Test]
		public void GetStaticField()
		{
			var src = @"Type::EmptyTypes";
			Test(src, Type.EmptyTypes);
		}

		[Test]
		public void GetArrayLength()
		{
			var src = @"(new [13; 37]).Length";
			Test(src, 2);
		}

		[Test]
		public void Dates()
		{
			Test("DateTime::MaxValue", DateTime.MaxValue);
			Test("DateTime::MinValue", DateTime.MinValue);
		}

		[Test]
		public void Literals()
		{
			Test("int::MaxValue", int.MaxValue);
			Test("int::MinValue", int.MinValue);

			Test("long::MaxValue", long.MaxValue);
			Test("long::MinValue", long.MinValue);

			Test("float::MaxValue", float.MaxValue);
			Test("float::MinValue", float.MinValue);

			Test("double::MaxValue", double.MaxValue);
			Test("double::MinValue", double.MinValue);

			Test("Byte::MaxValue", byte.MaxValue);
			Test("Byte::MinValue", byte.MinValue);
			Test("SByte::MaxValue", sbyte.MaxValue);
			Test("SByte::MinValue", sbyte.MinValue);
			Test("Int16::MaxValue", short.MaxValue);
			Test("Int16::MinValue", short.MinValue);
			Test("UInt16::MaxValue", ushort.MaxValue);
			Test("UInt16::MinValue", ushort.MinValue);
			Test("UInt32::MaxValue", uint.MaxValue);
			Test("UInt32::MinValue", uint.MinValue);
			Test("UInt64::MaxValue", ulong.MaxValue);
			Test("UInt64::MinValue", ulong.MinValue);

			Test("double::PositiveInfinity", double.PositiveInfinity);
			Test("double::NegativeInfinity", double.NegativeInfinity);
			Test("double::NaN", double.NaN);
		}

		[Test]
		public void GetFunc()
		{
			var src = @"
var fx = double::IsInfinity
fx (1.0 / 0)";

			Test(src, true);
		}

		[Test]
		public void MethodGenerics()
		{
			var src = "Enumerable::Empty<int> ()";
			Test(src, Enumerable.Empty<int>());
		}

		[Test]
		public void ImplicitValueTypeCtors()
		{
			Test("new int ()", 0);
			Test("new long ()", 0);
			Test("new float ()", 0.0f);
			Test("new double ()", 0.0);

			Test("(new Point ()).IsEmpty", true);
		}

		[Test]
		public void SetField()
		{
			var src = @"
var pt = new Point ()
pt.X = 10
pt.Y = 20
pt.IsEmpty";

			Test(src, false);
		}

		[Test]
		public void TryCatch()
		{
			var src = @"
var msg = 1
try
    var zero = 0
    1 / zero
catch (DivisionByZeroException ex)
    msg = 2
msg
";
			Test(src, 2);
		}

		[Test]
		public void LongArgumentPassing()
		{
			var src = @"
string::Compare
    <| ""a""
    <| ""b""
";
			Test(src, -1);
		}

		[Test]
		public void Linq1()
		{
			var src = @"(new [1; 2; 3; 4; 5]).Where ((a:int) -> a > 2)";
			Test(src, new [] {3, 4, 5});
		}

		[Test]
		public void Linq2()
		{
			var src = @"
Enumerable::Range 1 10
    |> Where ((x:int) -> x % 2 == 0)
    |> Select ((x:int) -> x * 2)";

			var result = new[] { 4, 8, 12, 16, 20};
			Test(src, result);
		}

		[Test]
		public void ExtensionMethods()
		{
			var src = @"
var a = new [1; 2; 3; 4; 5]
a.Max ()";
			Test(src, 5);
		}

		[Test]
		public void CollectionTypeInference()
		{
			Test(@"new [null; null; ""test""]", new[] {null, null, "test"});
			Test(@"new <null; ""test2"">", new[] { null, "test2" });
			Test(@"new {1 => null; 2 => ""test2""}[2]", "test2");
		}

		[Test]
		public void DelegateCasting()
		{
			var src = @"
var ts = (-> Console::WriteLine 1) as ThreadStart
ts ()";
			Test(src, null);
		}

		[Test]
		public void DelegateCasting2()
		{
			var src = @"
var filter = ((x:int) -> x > 2) as Predicate<int>
var arr = new [1; 2; 3; 4; 5]
Array::FindAll arr filter";

			Test(src, new [] { 3, 4, 5 });
		}

		[Test]
		public void RecursiveDeclararion()
		{
			var src = @"fun fact of int a:int -> if (a == 0) 1 else 1 * fact(a-1)";
			Test(src, null);
		}

		[Test]
		public void Closure1()
		{
			var src = @"
var a = 0
var b = 2
var fx = (x:int) -> a = b * x
fx 3
a";
			Test(src, 6);
		}

		[Test]
		public void Closure2()
		{
			var src = @"
var result = 0
var x1 = 1
var fx1 = (a:int) ->
    x1 = x1 + 1
    var x2 = 1
    var fx2 = (b:int) ->
        x2 = x2 + 1
        result = x1 + x2 + b
    fx2 a
fx1 10
result";
			Test(src, 14);
		}

		[Test]
		public void Algebraic1()
		{
			var src = @"
type TestType
    Value of int

(new Value 1).Tag";

//			var src = new NodeBase[]
//			{
//				Expr.Type(
//					"TestType",
//					Expr.Label("Value", "int")
//				),
//
//				Expr.GetMember(
//					Expr.New("Value", Expr.Int(1)),
//					"Tag"
//				)
//			};

			Test(src, 1);
		}

		[Test]
		public void Algebraic2()
		{
			var src = @"
type TestType
    Small of int
    Large of int

var a = new Small 1
var b = new Large 100
a.Tag + b.Tag";

//			var src = new NodeBase[]
//			{
//				Expr.Type(
//					"TestType",
//					Expr.Label("Small", "int"),
//					Expr.Label("Large", "int")
//				),
//
//				Expr.Var("a", Expr.New("Small", Expr.Int(1))),
//				Expr.Var("b", Expr.New("Large", Expr.Int(100))),
//				Expr.Add(
//					Expr.GetMember(Expr.Get("a"), "Tag"),
//					Expr.GetMember(Expr.Get("b"), "Tag")
//				)
//			};

			Test(src, 101);
		}

		[Test]
		public void Algebraic3()
		{
			var src = @"
type TestType
    Small of int
    Large of int

fun part of TestType x:int ->
    if (x > 100)
        (new Large x) as TestType
    else
        new Small x

var a = part 10
new [ a is TestType; a is Small; a is Large ]";

//			var src = new NodeBase[]
//			{
//				Expr.Type(
//					"TestType",
//					Expr.Label("Small", "int"),
//					Expr.Label("Large", "int")
//				),
//				
//				Expr.Fun(
//					"part",
//					"TestType",
//					new [] { Expr.Arg("x", "int") },
//					Expr.Block(
//						Expr.If(
//							Expr.Greater(Expr.Get("x"), Expr.Int(100)),
//							Expr.Block(
//								Expr.Cast(
//									Expr.New("Large", Expr.Get("x")),
//									"TestType"
//								)
//							),
//							Expr.Block(
//								Expr.New("Small", Expr.Get("x"))
//							)
//						)
//					)
//				),
//				
//				Expr.Var("a", Expr.Invoke("part", Expr.Int(10))),
//				Expr.Array(
//					Expr.Is(Expr.Get("a"), "TestType"),
//					Expr.Is(Expr.Get("a"), "Small"),
//					Expr.Is(Expr.Get("a"), "Large")
//				)
//			};

			Test(src, new [] { true, true, false });
		}

		[Test]
		public void Records1()
		{
			var src = @"
record Holder
    A : int
    B : int

var a = new Holder 2 3
a.A * a.B
";

//			var src = new NodeBase[]
//			{
//				Expr.Record(
//					"Holder",
//					Expr.Field("A", "int"),
//					Expr.Field("B", "int")
//				),
//
//				Expr.Var(
//					"a",
//					Expr.New("Holder", Expr.Int(2), Expr.Int(3))
//				),
//				Expr.Mult(
//					Expr.GetMember(Expr.Get("a"), "A"),
//					Expr.GetMember(Expr.Get("a"), "B")
//				)
//			};

			Test(src, 6);
		}

		[Test]
		public void Records2()
		{
			var src = @"
record First
    A : int

record Second
    B : int

var a = new First 2
var b = new Second 3
a.A * b.B
";
//			var src = new NodeBase[]
//			{
//				Expr.Record("First", Expr.Field("A", "int")),
//				Expr.Record("Second", Expr.Field("B", "int")),
//
//				Expr.Var("a", Expr.New("First", Expr.Int(2))),
//				Expr.Var("b", Expr.New("Second", Expr.Int(3))),
//				Expr.Mult(
//					Expr.GetMember(Expr.Get("a"), "A"),
//					Expr.GetMember(Expr.Get("b"), "B")
//				)
//			};

			Test(src, 6);
		}

		[Test]
		public void RefFunction1()
		{
			var src = @"
var x = 0
int::TryParse ""100"" ref x
x";
//			var src = new NodeBase[]
//			{
//				Expr.Var("x", Expr.Int(0)),
//				Expr.Invoke(
//					"int", "TryParse",
//					Expr.Str("100"),
//					Expr.Ref(Expr.Get("x"))
//				),
//				Expr.Get("x")
//			};

			Test(src, 100);
		}

		[Test]
		public void RefFunction2()
		{
			var src = @"
fun test a:int x:ref int -> x = a * 2
var result = 0
test 21 (ref result)
result
";
//			var src = new NodeBase[]
//			{
//				Expr.Fun(
//					"test",
//					new [] { Expr.Arg("a", "int"), Expr.Arg("x", "int", true) },
//					Expr.Block(
//						Expr.Set(
//							"x",
//							Expr.Mult(Expr.Get("a"), Expr.Int(2))
//						)
//					)
//				),
//				Expr.Var("result", Expr.Int(0)),
//				Expr.Invoke("test", Expr.Int(21), Expr.Ref(Expr.Get("result"))),
//				Expr.Get("result")
//			};

			Test(src, 42);
		}

		[Test]
		public void HasValue()
		{
			var src = @"
var a = null as Nullable<int>
var b = 1 as Nullable<int>
new [ a.HasValue; b.HasValue ]
";

			Test(src, new [] { false, true });
		}

		[Test]
		public void ImplicitExtensionMethod()
		{
			var src = @"
fun add of int a:int b:int -> a + b

let x = add 1 2
let y = 1.add 2
x == y
";
//			var src = new NodeBase[]
//			{
//				Expr.Fun(
//					"add",
//					"int",
//					new [] { Expr.Arg("a", "int"), Expr.Arg("b", "int") },
//					Expr.Add(
//						Expr.Get("a"),
//						Expr.Get("b")
//					)
//				),
//
//				Expr.Let("x", Expr.Invoke("add", Expr.Int(1), Expr.Int(2))),
//				Expr.Let("y", Expr.Invoke( Expr.Int(1), "add", Expr.Int(2))),
//				Expr.Equal(Expr.Get("x"), Expr.Get("y"))
//			};

			Test(src, true);
		}

		private void Test(string src, object value)
		{
			Assert.AreEqual(value, new LensCompiler().Run(src));
		}

		private void Test(IEnumerable<NodeBase> nodes, object value)
		{
			Assert.AreEqual(value, new LensCompiler().Run(nodes));
		}

		private object Compile(string src)
		{
			return new LensCompiler().Run(src);
		}

		private object Compile(IEnumerable<NodeBase> src)
		{
			return new LensCompiler().Run(src);
		}
	}
}
