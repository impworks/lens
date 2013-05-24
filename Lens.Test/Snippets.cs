using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree;
using Lens.SyntaxTree.SyntaxTree;
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
fun test:int -> 10
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
var list = new [[1; 2; 3]]
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
while idx < 5 do
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
var res = while a < 10 do
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
catch ex:DivisionByZeroException
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
			var src = @"(new [1; 2; 3; 4; 5]).Where (a:int -> a > 2)";
			Test(src, new [] {3, 4, 5});
		}

		[Test]
		public void Linq2()
		{
			var src = @"
Enumerable::Range 1 10
    |> Where (x:int -> x % 2 == 0)
    |> Select (x:int -> x * 2)";

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
			Test(@"new [[null; ""test2""]]", new[] { null, "test2" });
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
var filter = (x:int -> x > 2) as Predicate<int>
var arr = new [1; 2; 3; 4; 5]
Array::FindAll arr filter";

			Test(src, new [] { 3, 4, 5 });
		}

		[Test]
		public void RecursiveDeclararion()
		{
			var src = @"fun fact:int (a:int) -> if a == 0 then 1 else 1 * fact(a-1)";
			Test(src, null);
		}

		[Test]
		public void Closure1()
		{
			var src = @"
var a = 0
var b = 2
var fx = x:int -> a = b * x
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
var fx1 = a:int ->
    x1 = x1 + 1
    var x2 = 1
    var fx2 = b:int ->
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

(Value 1).Tag";

			Test(src, 1);
		}

		[Test]
		public void Algebraic2()
		{
			var src = @"
type TestType
    Small of int
    Large of int

var a = Small 1
var b = Large 100
a.Tag + b.Tag";

			Test(src, 101);
		}

		[Test]
		public void Algebraic3()
		{
			var src = @"
type TestType
    Small of int
    Large of int

fun part:TestType (x:int) ->
    if x > 100 then
        (Large x) as TestType
    else
        Small x

var a = part 10
new [ a is TestType; a is Small; a is Large ]";

			Test(src, new [] { true, true, false });
		}

		[Test]
		public void Algebraic4()
		{
			var src = @"
type Test
    Value1
    Value2 of Test
    Value3 of Tuple<Test, string>

let v1 = Value1
let v2 = Value2 v1
let v3 = Value3 (new(v2 as Test; ""hello""))
new [v1 is Test; v2 is Test; v3 is Test]";

			Test(src, new[] {true, true, true});
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

			Test(src, 6);
		}

		[Test]
		public void RefFunction1()
		{
			var src = @"
var x = 0
int::TryParse ""100"" ref x
x";

			Test(src, 100);
		}

		[Test]
		public void RefFunction2()
		{
			var src = @"
fun test (a:int x:ref int) -> x = a * 2

var result = 0
test 21 (ref result)
result
";

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
fun add:int (a:int b:int) -> a + b

let x = add 1 2
let y = 1.add 2
x == y
";

			Test(src, true);
		}

		[Test]
		public void DelegateTypeHints()
		{
			var src = new NodeBase[]
			{
				Expr.Var("test", Expr.GetMember("string", "Concat", "string", "string")),
				Expr.Invoke(Expr.Get("test"), Expr.Str("a"), Expr.Str("b"))
			};

			Test(src, "ab");
		}

		[Test]
		public void CustomIf()
		{
			var src = @"
type Clauses
    Else

fun If:bool (x:bool t:Action s:Else f:Action) ->
    let tAction = ->
        t ()
        true
    let fAction = ->
        f ()
        false
    x && tAction()
    x || fAction()

var res = string::Empty
If (1 > 2) (-> res = ""a"") Else (-> res = ""b"")
res
";

			Test(src, "b");
		}

		[Test]
		public void CustomWhile()
		{
			var src = @"
fun While (cond:Func<bool> body:Action) ->
    var act = null as Action
    act = ->
        if cond () then
            body ()
            act ()
    act ()

var result = new List<int> ()
var idx = 0
While 
    <| -> idx < 10
    <| ->
        result.Add idx
        idx = idx + 1

result.Count
";

			Test(src, 10);
		}

		[Test]
		public void GenericWithCustom1()
		{
			var src = @"
record MyRecord
    Value : int

var r1 = new MyRecord 1
var r2 = new MyRecord 2
var res = new (r1; r2)
res.Item1.Value + res.Item2.Value
";

			Test(src, 3);
		}

		[Test]
		public void GenericWithCustom2()
		{
			var src = @"
record MyRecord
    Value : int

var list = new [[new MyRecord 1; new MyRecord 2; new MyRecord 3]]
list.Count
";
			Test(src, 3);
		}

		[Test]
		public void GenericWithCustom3()
		{
			var src = @"
record MyRecord
    Value : int

var list = new [[new MyRecord 1; new MyRecord 2; new MyRecord 3]]
list[1].Value + list[2].Value
";
			Test(src, 5);
		}

		[Test]
		public void BuiltInComparison1()
		{
			var src = @"
record MyRecord
    Integer : int
    Stringy : string

var r1 = new MyRecord 4 ""lol""
var r2 = new MyRecord (2 + 2) (""l"" + ""ol"")
r1 == r2
";

			Test(src, true);
		}

		[Test]
		public void BuiltInComparison2()
		{
			var src = @"
type MyType
    Test of string

var r1 = Test ""hi""
var r2 = Test (""HI"".ToLower ())
r1 == r2
";

			Test(src, true);
		}

		[Test]
		public void CustomTypeLinq1()
		{
			var src = @"
record Store
    Name : string
    Value : int

var found = new [new Store ""a"" 1; new Store ""b"" 2; new Store ""c"" 3]
    |> Where (x:Store -> x.Value < 3)
    |> OrderByDescending (x:Store -> x.Value)
    |> First ()

found.Name
";
			Test(src, "b");
		}

		[Test]
		public void CustomTypeLinq2()
		{
			var src = @"
record Store
    Value : int

var data = new [new Store 1; new Store 1; new Store 40]
data.Sum (x:Store -> x.Value)
";
			Test(src, 42);
		}

		[Test]
		public void UninitializedVariables()
		{
			var code = new NodeBase[]
			{
				Expr.Var("a", "int"),
				Expr.Var("b", "string"),
				Expr.Var("c", "Point"),
				Expr.Array(
					Expr.Equal(Expr.Get("a"), Expr.Int(0)),
					Expr.Equal(Expr.Get("b"), Expr.Null()),
					Expr.Equal(Expr.Get("c"), Expr.New("Point"))
				)
			};

			Test(code, new [] { true, true, true });
		}

		[Test]
		public void TryFinally()
		{
			var code = new NodeBase[]
			{
				Expr.Var("a", "int"),
				Expr.Var("b", "int"),
				Expr.Try(
					Expr.Block(
						Expr.Var("c", Expr.Int(0)),
						Expr.Div(Expr.Int(1), Expr.Get("c"))
					),
					Expr.Block(
						Expr.Set("a", Expr.Int(1))
					),
					Expr.CatchAll(
						Expr.Set("b", Expr.Int(2))
					)
				),
				Expr.Array(
					Expr.Get("a"),
					Expr.Get("b")
				)
			};

			Test(code, new [] { 1, 2 });
		}

		[Test]
		public void ForLoop1()
		{
			var code = new NodeBase[]
			{
				Expr.Var("a", Expr.Str("")),
				Expr.For(
					"i",
					Expr.Int(5),
					Expr.Int(1),
					Expr.Block(
						Expr.Set(
							"a",
							Expr.Add(
								Expr.Get("a"),
								Expr.Invoke(
									Expr.Get("i"),
									"ToString"
								)
							)
						)
					)
				),
				Expr.Get("a")
			};

			Test(code, "54321");
		}

		private void Test(string src, object value)
		{
			Assert.AreEqual(value, Compile(src));
		}

		private void Test(IEnumerable<NodeBase> nodes, object value)
		{
			Assert.AreEqual(value, Compile(nodes));
		}

		private object Compile(string src)
		{
			var opts = new LensCompilerOptions {AllowSave = true};
			return new LensCompiler(opts).Run(src);
		}

		private object Compile(IEnumerable<NodeBase> src)
		{
			var opts = new LensCompilerOptions { AllowSave = true };
			return new LensCompiler(opts).Run(src);
		}
	}
}
