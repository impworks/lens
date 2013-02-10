using System.Collections.Generic;
using Lens.SyntaxTree.Utils;
using Lens.Test.TestClassHierarchy;
using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class TypeExtensionsTest
	{
		[Test]
		public void SelfEquality()
		{
			TestDistanceFrom<int, int>(0);
			TestDistanceFrom<object, object>(0);
			TestDistanceFrom<List<int>, List<int>>(0);
		}

		[Test]
		public void BoxTest()
		{
			TestDistanceFrom<object, int>(1);
			TestDistanceFrom<object, Struct>(1);
		}

		[Test]
		public void ParentTest()
		{
			TestDistanceFrom<ParentClass, DerivedClass>(1);
			TestDistanceFrom<DerivedClass, ParentClass>(int.MaxValue);
		}

		/// <summary>
		/// Checks if the <see cref="expected"/> value are equal to the <see cref="TypeExtensions.DistanceFrom"/> call
		/// result.
		/// </summary>
		/// <typeparam name="T1">First type.</typeparam>
		/// <typeparam name="T2">Second type.</typeparam>
		/// <param name="expected">Expected distance between types.</param>
		private static void TestDistanceFrom<T1, T2>(int expected)
		{
			var result = typeof (T1).DistanceFrom(typeof (T2));
			Assert.AreEqual(expected, result);
		}
	}
}
