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
		[Test]
		public void BasicName()
		{
			Test("Uri", typeof(Uri));
		}

		[Test]
		public void ObjectAlias()
		{
			Test("object", typeof(object));
		}

		[Test]
		public void BoolAlias()
		{
			Test("bool", typeof(bool));
		}

		[Test]
		public void IntAlias()
		{
			Test("int", typeof(int));
		}

		[Test]
		public void DoubleAlias()
		{
			Test("double", typeof(double));
		}

		[Test]
		public void StringAlias()
		{
			Test("string", typeof(string));
		}

		[Test]
		public void Array()
		{
			Test("int[]", typeof(int[]));
		}

		[Test]
		public void MultipleArray()
		{
			Test("int[][][]", typeof(int[][][]));
		}

		[Test]
		public void LongName()
		{
			Test("System.Text.RegularExpressions.Regex", typeof(Regex));
		}

		[Test]
		public void NameFromNamespace()
		{
			Test("Regex", typeof(Regex));
		}

		[Test]
		public void GenericSimple()
		{
			Test("Dictionary<int, string>", typeof(Dictionary<int, string>));
		}

		[Test]
		public void GenericFull()
		{
			Test("System.Collections.Generic.Dictionary<System.Int32, System.String>", typeof(Dictionary<int, string>));
		}

		[Test]
		public void Nightmare()
		{
			Test("Dictionary<System.Uri, List<Tuple<int[], string>>>", typeof(Dictionary<Uri, List<Tuple<int[], string>>>));
		}

		[Test]
		public void SelfReference()
		{
			Test("Lens.SyntaxTree.Unit", typeof(Unit));
		}

		private static void Test(string signature, Type type)
		{
			Assert.AreEqual(new TypeResolver().ResolveType(signature), type);
		}
	}
}
