using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Compiler;
using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class TypeResolverTest
	{
		private static TypeResolver2 Resolver;
		static TypeResolverTest()
		{
			Resolver = new TypeResolver2();	
		}

		[Test]
		public void BasicName()
		{
			Test<Uri>("Uri");
			Test<Regex>("Regex");
		}

		[Test]
		public void Aliases()
		{
			Test<object>("object");
			Test<bool>("bool");
			Test<int>("int");
			Test<double>("double");
			Test<string>("string");
		}

		[Test]
		public void Array()
		{
			Test<int[]>("int[]");
			Test<int[][][]>("int[][][]");
		}

		[Test]
		public void LongName()
		{
			Test<Regex>("System.Text.RegularExpressions.Regex");
		}

		[Test]
		public void GenericSimple()
		{
			Test<Dictionary<int, string>>("Dictionary<int, string>");
		}

		[Test]
		public void GenericFull()
		{
			Test<Dictionary<int, string>>("System.Collections.Generic.Dictionary<System.Int32, System.String>");
		}

		[Test]
		public void Nightmare()
		{
			Test<Dictionary<Uri, List<Tuple<int[], string>>>>("Dictionary<System.Uri, List<Tuple<int[], string>>>");
		}

		[Test]
		public void SelfReference()
		{
			Test<Unit>("Lens.SyntaxTree.Unit");
		}

		private static void Test<T>(string signature)
		{
			Assert.AreEqual(new TypeResolver2().ResolveType(signature), typeof(T));

//			var casts = 100;
//			var to1 = DateTime.Now;
//			for (var idx = 0; idx < casts; idx++)
//				Assert.AreEqual(new TypeResolver().ResolveType(signature), type);
//			var to2 = DateTime.Now;
//
//			var tn1 = DateTime.Now;
//			for (var idx = 0; idx < casts; idx++)
//				Assert.AreEqual(new TypeResolver2().ResolveType(signature), type);
//			var tn2 = DateTime.Now;
//
//			Console.WriteLine("{0}: old = {1}, new = {2}", signature, (to2 - to1).TotalSeconds, (tn2 - tn1).TotalSeconds);
		}
	}
}
