using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lens.Utils;
using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class FunctionalHelperTest
	{
		[Test]
		public void IsFuncTest()
		{
			var cases = new Dictionary<Type, bool>
			{
				{ typeof(int), false },
				{ typeof(Action<>), false},
				{ typeof(Func<int>), true},
				{ typeof(Func<>), true},
				{ typeof(Func<,,,>), true},
				{ typeof(Func<int,string,int>), true}
			};

			foreach(var curr in cases)
				Assert.AreEqual(curr.Key.IsFuncType(), curr.Value);
		}

		[Test]
		public void IsActionTest()
		{
			var cases = new Dictionary<Type, bool>
			{
				{ typeof(int), false },
				{ typeof(Action<>), true},
				{ typeof(Action<,,,>), true},
				{ typeof(Action), true},
				{ typeof(Action<int>), true},
				{ typeof(Action<int,string,Func<int>>), true},
				{ typeof(Func<int>), false},
				{ typeof(Func<>), false},
			};

			foreach (var curr in cases)
				Assert.AreEqual(curr.Key.IsActionType(), curr.Value);
		}

		[Test]
		public void DiscardParametersTest1()
		{
			var t1 = typeof (Func<int, string, bool, double, float, long>);
			var t2 = typeof (Func<bool, double, float, long>);
			var t3 = typeof (Func<long>);

			Assert.AreEqual(t1.DiscardParameters(0), t1);
			Assert.AreEqual(t1.DiscardParameters(2), t2);
			Assert.AreEqual(t2.DiscardParameters(3), t3);

			Assert.Throws<LensCompilerException>(() => t2.DiscardParameters(4));
		}

		[Test]
		public void DiscardParametersTest2()
		{
			var t1 = typeof(Action<int, string, bool, double, float, long>);
			var t2 = typeof(Action<bool, double, float, long>);
			var t3 = typeof(Action<long>);
			var t4 = typeof(Action);

			Assert.AreEqual(t1.DiscardParameters(0), t1);
			Assert.AreEqual(t1.DiscardParameters(2), t2);
			Assert.AreEqual(t2.DiscardParameters(3), t3);
			Assert.AreEqual(t3.DiscardParameters(1), t4);

			Assert.Throws<LensCompilerException>(() => t2.DiscardParameters(5));
		}

		[Test]
		public void CreateActionTypeTest()
		{
			Assert.AreEqual(
				typeof(Action<int, string, TimeSpan>),
				FunctionalHelper.CreateActionType(typeof(int), typeof(string), typeof(TimeSpan))
			);

			Assert.AreEqual(typeof(Action), FunctionalHelper.CreateActionType());

			Assert.Throws<LensCompilerException>(() => FunctionalHelper.CreateActionType(new Type[20]));
		}

		[Test]
		public void CreateFuncTypeTest()
		{
			Assert.AreEqual(
				typeof(Func<int, string, TimeSpan>),
				FunctionalHelper.CreateFuncType(typeof(TimeSpan), typeof(int), typeof(string))
			);

			Assert.AreEqual(typeof(Func<bool>), FunctionalHelper.CreateFuncType(typeof(bool)));

			Assert.Throws<LensCompilerException>(() => FunctionalHelper.CreateFuncType(typeof(int), new Type[20]));
		}

		[Test]
		public void AddParametersTest1()
		{
			var t1 = typeof (Func<int>);
			var t2 = typeof (Func<long, bool, int>);
			var t3 = typeof (Func<string, Tuple<int, int>, long, bool, int>);

			Assert.AreEqual(t1.AddParameters(typeof(long), typeof(bool)), t2);
			Assert.AreEqual(t2.AddParameters(typeof(string), typeof(Tuple<int, int>)), t3);

			Assert.Throws<LensCompilerException>(() => t1.AddParameters(new Type[20]));
		}

		[Test]
		public void AddParametersTest2()
		{
			var t1 = typeof(Action);
			var t2 = typeof(Action<int>);
			var t3 = typeof(Action<long, bool, int>);
			var t4 = typeof(Action<string, Tuple<int, int>, long, bool, int>);

			Assert.AreEqual(t1.AddParameters(typeof(int)), t2);
			Assert.AreEqual(t2.AddParameters(typeof(long), typeof(bool)), t3);
			Assert.AreEqual(t3.AddParameters(typeof(string), typeof(Tuple<int, int>)), t4);

			Assert.Throws<LensCompilerException>(() => t1.AddParameters(new Type[20]));
		}

		[Test]
		public void ExternalDelegates()
		{
			Assert.IsTrue(typeof(ThreadStart).IsCallableType());
			Assert.IsTrue(typeof(ParameterizedThreadStart).IsCallableType());
		}
	}
}
