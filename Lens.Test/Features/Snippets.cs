using System;
using System.Linq;
using NUnit.Framework;

namespace Lens.Test.Features
{
	[TestFixture]
	internal class Snippets : TestBase
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
		public void InvokeDynamic1()
		{
			Test(@"1.GetHashCode ()", 1);
		}


		[Test]
		public void InvokeDynamic2()
		{
			Test(@"(1+2).GetHashCode ()", 3);
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

			Test("(new System.Drawing.Point ()).IsEmpty", true);
		}

		[Test]
		public void SetField()
		{
			var src = @"
var pt = new System.Drawing.Point ()
pt.X = 10
pt.Y = 20
pt.IsEmpty";

			Test(src, false);
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
			Test(@"(new {1 => null; 2 => ""test2""})[2]", "test2");
		}

		[Test]
		public void HasValue()
		{
			var src = @"
var a = null as int?
var b = 1 as int?
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
			var src = @"
var test = string::Concat<string, string>
test ""a"" ""b""
";

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
		public void UninitializedVariables()
		{
			var src = @"
var a : int
var b : string
var c : System.Drawing.Point

new [a == 0; b == null; c == (new System.Drawing.Point ())]";

			Test(src, new [] { true, true, true });
		}

		[Test]
		public void Shift()
		{
			var src = @"
new [
    2 <: 1
    3 <: 4
    32 :> 1
    18 :> 2
    10 :> 10
]
";

			Test(src, new [] { 4, 48, 16, 4, 0 });
		}

		[Test]
		public void FieldOfProperty()
		{
			var src = @"DateTime::Now.Year";
			Test(src, DateTime.Now.Year);
		}

		[Test]
		public void RefArray()
		{
			var src = @"
fun inc (x:ref int) ->
    x = x + 1

var data = new [1; 2; 2]
inc (ref data[2])
data";

			Test(src, new[] {1, 2, 3});
		}

		[Test]
		public void ScopeNames1()
		{
			var src = @"
var data = new[1; 2; 3; 4; 5]
var res = new List<int> ()
for x in data do
    if x % 2 == 0 then
        let p = x * 2
        res.Add p
    else
        let p = x * 3
        res.Add p
res
";

			Test(src, new[] { 3, 4, 9, 8, 15 });
		}

		[Test]
		public void ScopeNames2()
		{
			var src = @"
var funcs = new List<Func<int>> ()
for x in Enumerable::Range 1 3 do
    funcs.Add (-> x * 2)

funcs.Select (fx -> fx ())
";
			Test(src, new[] {2, 4, 6});
		}

		[Test]
		public void ComplexEnumerables()
		{
			// todo:
			// IOrderedEnumerable<T> extends IEnumerable<T>, therefore inherits GetEnumerator () method
			// howerver, GetMethods() doesn't work on interfaces
			// and TypeBuilder.GetMethod(type, method) doesn't work if `type` = IOrderedEnumerable and `method.DeclaringType` = IEnumerable

			var src = @"
record Store
    Name : string
    Stock : int

let stores = new [
    new Store ""A"" 10
    new Store ""B"" 42
    new Store ""C"" 5
]

let order = stores.OrderByDescending (x -> x.Stock)

var names = new List<string> ()
for s in order do
    names.Add (s.Name + "":"" + s.Stock)
";
			Test(src, new [] { "B:42", "A:10", "C:5" });
		}
	}
}
