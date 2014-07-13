using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using NUnit.Framework;

namespace Lens.Test.Internals
{
	[TestFixture]
	public class ExtensionMethodResolverTest
	{
		[Test]
		public void TestEnumerable1()
		{
			Test(typeof(IEnumerable<int>), "Where", new[] { typeof(Func<int, bool>) }, typeof(Enumerable));
		}

		[Test]
		public void TestEnumerable2()
		{
			Test(typeof(string[]), "Select", new [] { typeof(Func<string, int>) }, typeof(Enumerable));
		}

		[Test]
		public void TestEnumerable3()
		{
			Test(typeof (int[]), "Max", Type.EmptyTypes, typeof(Enumerable));
		}

		private void Test(Type type, string name, Type[] args, Type ethalonType)
		{
			var rr = new ReflectionResolver();
			var res = new ExtensionMethodResolver(rr, new Dictionary<string, bool> {{"System", true}, {"System.Linq", true}}, new ReferencedAssemblyCache());
			var found = res.ResolveExtensionMethod(type, name, args);
			var bucket = ethalonType.GetMethods().Where(m => m.Name == name).ToArray();
			Assert.Contains(found, bucket);
		}
	}
}
