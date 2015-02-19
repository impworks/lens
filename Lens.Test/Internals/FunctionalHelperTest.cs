using System;
using System.Collections.Generic;
using System.Threading;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Utils;
using NUnit.Framework;

namespace Lens.Test.Internals
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
				{typeof (int), false},
				{typeof (Action<>), true},
				{typeof (Action<,,,>), true},
				{typeof (Action), true},
				{typeof (Action<int>), true},
				{typeof (Action<int, string, Func<int>>), true},
				{typeof (Func<int>), false},
				{typeof (Func<>), false},
			};

			foreach (var curr in cases)
				Assert.AreEqual(curr.Key.IsActionType(), curr.Value);
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
		public void CreateLambdaTypeTest()
		{
			Assert.AreEqual(
				typeof(Lambda<int, string, TimeSpan>),
				FunctionalHelper.CreateLambdaType(typeof(int), typeof(string), typeof(TimeSpan))
			);

			Assert.AreEqual(typeof(Lambda<bool>), FunctionalHelper.CreateLambdaType(typeof(bool)));
			Assert.AreEqual(typeof(Func<UnspecifiedType>), FunctionalHelper.CreateLambdaType());

			Assert.Throws<LensCompilerException>(() => FunctionalHelper.CreateLambdaType(new Type[20]));
		}

		[Test]
		public void ExternalDelegates()
		{
			Assert.IsTrue(typeof(ThreadStart).IsCallableType());
			Assert.IsTrue(typeof(ParameterizedThreadStart).IsCallableType());
			Assert.IsFalse(typeof(string).IsCallableType());
		}
	}
}
