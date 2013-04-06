using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Utils;
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
			TestDistanceFrom<Action<int>, Action<object>>(int.MaxValue);
		}

		[Test]
		public void CovarianceTest()
		{
			TestDistanceFrom<IEnumerable<ParentClass>, IEnumerable<DerivedClass>>(1);
			TestDistanceFrom<IEnumerable<object>, IEnumerable<bool>>(int.MaxValue);
		}

		[Test]
		public void MultiArgumentGeneric()
		{
			TestDistanceFrom<Action<DerivedClass, DerivedClass>, Action<ParentClass, ParentClass>>(2);
			TestDistanceFrom<Func<DerivedClass, ParentClass>, Func<ParentClass, DerivedClass>>(2);
		}

		[Test]
		public void DecimalNotSupported()
		{
			TestDistanceFrom<decimal, sbyte>(int.MaxValue);
			TestDistanceFrom<decimal, ulong>(int.MaxValue);
		}

		[Test]
		public void IntegralTypeConversion()
		{
			TestDistanceFrom<long, sbyte>(3);
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
			// int[] -> Array -> object
			TestDistanceFrom<object, int[]>(2);
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

		[Test]
		public void NumericOperationTypes()
		{
			AssertNumericOperationType<int, int, int>();
			AssertNumericOperationType<long, long, long>();
			AssertNumericOperationType<float, float, float>();
			AssertNumericOperationType<double, double, double>();
			AssertNumericOperationType<byte, byte, int>();
			AssertNumericOperationType<short, short, int>();

			AssertNumericOperationType<float, int, float>();
			AssertNumericOperationType<float, long, double>();

			AssertNumericOperationNotPermitted<int, uint>();
			AssertNumericOperationNotPermitted<int, ulong>();
		}

		[Test]
		public void ImplicitCast()
		{
			TestDistanceFrom<int, ImplicitCastable>(1);
		}

		[Test]
		public void ArrayInterface()
		{
			TestDistanceFrom<IEnumerable<int>, int[]>(1);
		}

		[Test]
		public void GenericParameter()
		{
			var from = typeof(bool[]);
			var to = typeof (IEnumerable<>);
			Assert.AreEqual(2, to.DistanceFrom(from));
		}

		[Test]
		public void Generic()
		{
			TestDistanceFrom<List<float>, List<int>>(int.MaxValue);
			TestDistanceFrom<List<ParentClass>, List<DerivedClass>>(int.MaxValue);
			TestDistanceFrom<IEnumerable<float>, IEnumerable<int>>(int.MaxValue);
			TestDistanceFrom<IEnumerable<ParentClass>, IEnumerable<DerivedClass>>(1);
		}

		[Test]
		public void GenericParameter2()
		{
			var from1 = typeof (int[]);
			var from2 = typeof (Predicate<int>);

			var to = typeof (Array).GetMethod("FindAll").GetParameters().Select(p => p.ParameterType).ToArray();

			Assert.AreEqual(1, to[0].DistanceFrom(from1));
			Assert.AreEqual(1, to[1].DistanceFrom(from2));
		}

		[Test]
		public void RefArguments()
		{
			var types = new[] {typeof (object), typeof (float), typeof (int), typeof (string)};
			foreach (var type in types)
				Assert.AreEqual(0, type.MakeByRefType().DistanceFrom(type));

			Assert.AreEqual(int.MaxValue, typeof(int).MakeByRefType().DistanceFrom(typeof(float)));
			Assert.AreEqual(int.MaxValue, typeof(float).MakeByRefType().DistanceFrom(typeof(int)));
		}

		[Test]
		public void Interface()
		{
			TestDistanceFrom<object, IEnumerable<int>>(1);
			TestDistanceFrom<object, IEnumerable<DerivedClass>>(1);
		}

		[Test]
		public void CommonNumericTypes()
		{
			TestCommonType<int>(typeof(int), typeof(int), typeof(int));
			TestCommonType<float>(typeof(float), typeof(int), typeof(int));
			TestCommonType<long>(typeof(int), typeof(int), typeof(int), typeof(long));
			TestCommonType<double>(typeof(float), typeof(int), typeof(long));
			TestCommonType<double>(typeof(float), typeof(int), typeof(double));
		}

		[Test]
		public void CommonTypes()
		{
			TestCommonType<ParentClass>(typeof(ParentClass), typeof(ParentClass));
			TestCommonType<ParentClass>(typeof(DerivedClass), typeof(ParentClass));
			TestCommonType<ParentClass>(typeof(DerivedClass), typeof(DerivedClass2));
			TestCommonType<ParentClass>(typeof(SubDerivedClass), typeof(DerivedClass2));
		}

		[Test]
		public void CommonArrayTypes()
		{
			TestCommonType<int[]>(typeof(int[]), typeof(int[]));
			TestCommonType<IList>(typeof(float[]), typeof(int[]));
			TestCommonType<ParentClass[]>(typeof(ParentClass[]), typeof(DerivedClass[]));
			TestCommonType<ParentClass[]>(typeof(DerivedClass[]), typeof(DerivedClass2[]));
		}

		[Test]
		public void CommonInterfaces()
		{
			TestCommonType<IList>(typeof(float[]), typeof(List<int>));
			TestCommonType<IList<int>>(typeof(int[]), typeof(List<int>));
			TestCommonType<IEnumerable<int>>(typeof(int[]), typeof(IEnumerable<int>));
			TestCommonType<IEnumerable<int>>(typeof(List<int>), typeof(IEnumerable<int>));
		}

		[Test]
		public void CommonObjectOnly()
		{
			TestCommonType<object>(typeof(ParentClass), typeof(DerivedClass2), typeof(IEnumerable<int>));
			TestCommonType<object>(typeof(object), typeof(int));
			TestCommonType<object>(typeof(int), typeof(float), typeof(double), typeof(Decimal));
			TestCommonType<object>(typeof(IEnumerable<int>), typeof(IEnumerable<float>));
		}

		/// <summary>
		/// Checks if the <see cref="expected"/> value are equal to the <see cref="TypeExtensions.DistanceFrom"/> call  result.
		/// </summary>
		/// <typeparam name="T1">First type.</typeparam>
		/// <typeparam name="T2">Second type.</typeparam>
		/// <param name="expected">Expected distance between types.</param>
		private static void TestDistanceFrom<T1, T2>(int expected)
		{
			var result = typeof (T1).DistanceFrom(typeof (T2));
			Assert.AreEqual(expected, result);
		}

		private static void AssertNumericOperationType<T1, T2, TResult>()
		{
			Assert.AreEqual(typeof (TResult), TypeExtensions.GetNumericOperationType(typeof (T1), typeof (T2)));
		}

		private static void AssertNumericOperationNotPermitted<T1, T2>()
		{
			Assert.IsNull(TypeExtensions.GetNumericOperationType(typeof (T1), typeof (T2)));
		}

		private static void TestCommonType<T>(params Type[] types)
		{
			Assert.AreEqual(typeof (T), types.GetMostCommonType());
		}
	}
}
