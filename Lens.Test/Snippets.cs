using System;
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
fun test -> 10
test ()";
			Test(src, null);
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
			Assert.Throws<NotImplementedException>(() =>Compile(src));
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
