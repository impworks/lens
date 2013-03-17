using System;
using System.Linq;
using Lens.SyntaxTree;
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
			Test("Enumerable::Empty<int> ()", Enumerable.Empty<int>());
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
		public void Linq()
		{
			var src = @"new [1; 2; 3; 4; 5].Where ((a:int) -> a > 2)";
			Test(src, new [] {3, 5, 5});
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

		private void Test(string src, object value)
		{
			Assert.AreEqual(value, Compile(src));
		}

		private object Compile(string src)
		{
			return new LensCompiler().Run(src);
		}
	}
}
