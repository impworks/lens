using System;
using System.Collections.Generic;
using System.Text;
using Lens.SyntaxTree;
using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class OperatorsTest
	{
		[Test]
		public void TypeofTest()
		{
			TestType<int>("typeof(int)");
			TestType<float>("typeof(float)");
			TestType<object>("typeof(object)");

			TestType<Dictionary<int, Exception>>("typeof(Dictionary<int, System.Exception>)");
		}

		[Test]
		public void DefaultTest()
		{
			Test("default(int)", 0);
			Test("default(float)", 0.0f);
			Test("default(long)", 0L);
			Test("default(bool)", false);

			Test("default(string)", null);

			Test("default(Nullable<int>)", default(int?));
		}

		[Test]
		public void CastTest()
		{
			Test("0 as float", 0.0f);
			Test("0.0 as int", 0);
			Test("1.23 as long", 1L);
			Test("1 as Nullable<int>", 1);
			Test("null as string", null);
			Test("null as Nullable<int>", new int?());
		}
		
		[Test]
		public void NewObjectTest()
		{
			Assert.IsInstanceOf<StringBuilder>(Compile("new System.Text.StringBuilder ()"));
			Assert.IsInstanceOf<List<int>>(Compile("new List<int> ()"));

			Test("new Decimal 1", new Decimal(1));
			Test(@"new Uri ""http://test.ru""", new Uri("http://test.ru"));
			Test(@"new Tuple<int, string> 1 ""hello""", new Tuple<int, string>(1, "hello"));
		}

		[Test]
		public void ArithmeticsTest()
		{
			Test("1 + 2", 3);
			Test("13 + 0.37", 13.37);
			Test("1336.9 + 0.1", 1337);
			Test("(1336 as UInt32) + (1 as UInt32)", 1337);

			Test("43 - 1", 42);

			Test("21 * 2", 42);
			Test("1.5 * 1.5", 2.25);

			Test("84 / 2", 42);

			Test("92 % 50", 42);

			Assert.Throws<LensCompilerException>(() => Compile("1 + (1 as UInt32)"));
		}

		[Test]
		public void StringConcat()
		{
			Test(@"""a"" + ""b""", "ab");
			Test(@"""a"" + ""b"" + ""c""", "abc");
		}

		private void Test(string src, object value)
		{
			Assert.AreEqual(Compile(src), value);
		}

		private void TestType<T>(string src)
		{
			var obj = Compile(src);
			Assert.AreEqual(obj, typeof(T));
		}

		private object Compile(string src)
		{
			return new LensCompiler().Run(src);
		}
	}
}
