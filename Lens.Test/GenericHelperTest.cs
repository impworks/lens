using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Compiler;
using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class GenericHelperTest
	{
		[Test]
		public void Unbound()
		{
			Test(typeof (int), "UnboundGeneric", new[] {typeof (int)}, new[] {typeof (int)});
			Assert.Throws<TypeMatchException>(() => Test(typeof(int), "UnboundGeneric", new[] { typeof(int) }));
		}

		[Test]
		public void Bound()
		{
			Test(typeof(string), "BoundGeneric", new [] { typeof(int)}, new [] { typeof(string) } );
		}

		[Test]
		public void Parameteric()
		{
			Test(typeof(int), "Parametric", new [] { typeof(int), typeof(int) });
			Test(typeof(int), "Parametric", new[] { typeof(int), typeof(int) }, new [] { typeof(int) });
			Assert.Throws<TypeMatchException>(() => Test(typeof (int), "Parametric", new[] {typeof (int), typeof (string)}));
		}

		[Test]
		public void Deep()
		{
			Test(typeof(Tuple<bool, float>), "Deep", new[] { typeof(bool), typeof(IEnumerable<float>) });
		}

		[Test]
		public void Nightmare()
		{
			Test(
				typeof(Dictionary<Tuple<int, bool, int>, IEnumerable<string>>),
				"Nightmare",
				new[] { typeof(int), typeof(List<Tuple<bool, bool>>) },
				new[] { null, null, typeof(string) }
			);
		}

		private void Test(Type desired, string name, Type[] args = null, Type[] hints = null)
		{
			var method = typeof (GenericHelperTestExample).GetMethods().Single(m => m.Name == name);
			var defs = method.GetGenericArguments();
			var values = GenericHelper.ResolveMethodGenericsByArgs(
				method.GetParameters().Select(p => p.ParameterType).ToArray(),
				args,
				defs,
				hints
			);
			var newMethod = method.MakeGenericMethod(values);
			Assert.AreEqual(desired, newMethod.ReturnType);
		}
	}

	public static class GenericHelperTestExample
	{
		public static int UnboundGeneric<T>(int a) { return a; }

		public static T BoundGeneric<T>(int a) { return default(T); }

		public static int Parametric<T>(T a, T b) { return 1; }

		public static Tuple<T1, T2> Deep<T1, T2>(T1 arg1, IEnumerable<T2> arg2) { return default(Tuple<T1, T2>); }

		public static Dictionary<Tuple<T1, T2, int>, IEnumerable<T3>> Nightmare<T1, T2, T3>(T1 a, List<Tuple<T2, bool>> b) { return null; }

		public static IEnumerable<T> Extension<T>(T obj, int args) { return null; }
	}
}
