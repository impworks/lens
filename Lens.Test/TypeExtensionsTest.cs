using System;
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
		public void ParentTest()
		{
			TestDistanceFrom<ParentClass, DerivedClass>(1);
			TestDistanceFrom<DerivedClass, ParentClass>(int.MaxValue);
		}

		[Test]
		public void BoxTest()
		{
			TestDistanceFrom<object, int>(1);
			TestDistanceFrom<object, Struct>(1);
		}

		[Test]
		public void InterfaceTest()
		{
			TestDistanceFrom<IInterface, InterfaceImplementer>(1);
			TestDistanceFrom<IInterface, InterfaceDerivedImplementer>(1);
			TestDistanceFrom<IInterface, DerivedInterfaceImplementer>(1);
		}

		[Test]
		public void ContravarianceTest()
		{
			TestDistanceFrom<Action<DerivedClass>, Action<ParentClass>>(1);
			TestDistanceFrom<Action<int>, Action<object>>(1);
		}

		[Test]
		public void CovarianceTest()
		{
			TestDistanceFrom<IEnumerable<ParentClass>, IEnumerable<DerivedClass>>(1);
			TestDistanceFrom<IEnumerable<object>, IEnumerable<bool>>(1);
		}

		[Test]
		public void MultiArgumentGeneric()
		{
			TestDistanceFrom<Action<DerivedClass, DerivedClass>, Action<ParentClass, ParentClass>>(2);
			TestDistanceFrom<Func<DerivedClass, ParentClass>, Func<ParentClass, DerivedClass>>(2);
		}

		[Test]
		public void IntegralTypeConversion()
		{
			TestDistanceFrom<long, sbyte>(3);
			TestDistanceFrom<decimal, sbyte>(4);
		}

		[Test]
		public void FloatingPointTypeConversion()
		{
			TestDistanceFrom<double, float>(1);
		}

		[Test]
		public void CrossDomainNumberConversion()
		{
			TestDistanceFrom<float, int>(1);
			TestDistanceFrom<double, long>(1);
			TestDistanceFrom<double, short>(3);
		}

		[Test]
		public void UnsignedToSignedConversion()
		{
			TestDistanceFrom<long, uint>(1);
			TestDistanceFrom<uint, int>(int.MaxValue);
			TestDistanceFrom<decimal, ulong>(1);
		}

		[Test]
		public void UnsignedToFloatConversion()
		{
			TestDistanceFrom<float, ushort>(2);
			TestDistanceFrom<double, uint>(2);
		}

		[Test]
		public void IllegalConversionsTest()
		{
			TestDistanceFrom<sbyte, short>(int.MaxValue);
			TestDistanceFrom<sbyte, int>(int.MaxValue);
			TestDistanceFrom<sbyte, long>(int.MaxValue);
			TestDistanceFrom<sbyte, decimal>(int.MaxValue);
			TestDistanceFrom<sbyte, float>(int.MaxValue);
			TestDistanceFrom<sbyte, double>(int.MaxValue);
			TestDistanceFrom<sbyte, byte>(int.MaxValue);
			TestDistanceFrom<sbyte, ushort>(int.MaxValue);
			TestDistanceFrom<sbyte, uint>(int.MaxValue);
			TestDistanceFrom<sbyte, ulong>(int.MaxValue);

			TestDistanceFrom<short, int>(int.MaxValue);
			TestDistanceFrom<short, long>(int.MaxValue);
			TestDistanceFrom<short, decimal>(int.MaxValue);
			TestDistanceFrom<short, float>(int.MaxValue);
			TestDistanceFrom<short, double>(int.MaxValue);
			TestDistanceFrom<short, ushort>(int.MaxValue);
			TestDistanceFrom<short, uint>(int.MaxValue);
			TestDistanceFrom<short, ulong>(int.MaxValue);

			TestDistanceFrom<int, long>(int.MaxValue);
			TestDistanceFrom<int, decimal>(int.MaxValue);
			TestDistanceFrom<int, float>(int.MaxValue);
			TestDistanceFrom<int, double>(int.MaxValue);
			TestDistanceFrom<int, uint>(int.MaxValue);
			TestDistanceFrom<int, ulong>(int.MaxValue);

			TestDistanceFrom<long, ulong>(int.MaxValue);
			TestDistanceFrom<long, float>(int.MaxValue);
			TestDistanceFrom<long, double>(int.MaxValue);
			TestDistanceFrom<long, decimal>(int.MaxValue);
		}

		[Test]
		public void ArrayAsObjectDistance()
		{
			TestDistanceFrom<object, int[]>(1);
		}

		[Test]
		public void ArrayCovariance()
		{
			TestDistanceFrom<object[], string[]>(1);
		}

		[Test]
		public void Nullable()
		{
			TestDistanceFrom<int?, int>(1);
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
