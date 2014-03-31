using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lens.Compiler;
using Lens.Utils;
using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class TypeResolverTest
	{
		private static readonly TypeResolver Resolver = new TypeResolver(new Dictionary<string, bool> { { "System", true } });

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
			Test<Unit>("Lens.Unit");
		}

		[Test]
		public void DefaultNamespaces()
		{
			Assert.AreEqual(Resolver.ResolveType("Enumerable"), typeof(System.Linq.Enumerable));
		}

		[Test]
		public void Generics2()
		{
			Assert.Throws<LensCompilerException>(() => Resolver.ResolveType("Tuple<_>[]"));
			Assert.Throws<LensCompilerException>(() => Resolver.ResolveType("Tuple<int, Predicate<_>>"));
		}

		[Test]
		public void Nullable()
		{
			Test<int?>("int?");
			Test<bool?>("bool?");
			Assert.Throws<TypeMatchException>(() => Resolver.ResolveType("string?"));
		}

		[Test]
		public void IEnumerable()
		{
			Test<IEnumerable<int>>("int~");
			Test<IEnumerable<string>>("string~");
		}

		[Test]
		public void CompoundPostfixes()
		{
			Test<IEnumerable<Nullable<int>>[]>("int?~[]");
		}

		private static void Test<T>(string signature)
		{
			Assert.AreEqual(Resolver.ResolveType(signature), typeof(T));
		}
	}
}
