using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
	[TestFixture]
	internal class CastingTest : TestBase
	{
		[Test]
		public void NumericTypesUpcast()
		{
			Test("1 as double", 1.0);
			Test("1 as float", 1.0f);
			Test("1 as long", 1L);
			Test("1 as decimal", 1M);

			Test("1.0f as double", 1.0);
			Test("1.2f as decimal", 1.2M);

			Test("1.5 as decimal", 1.5M);
		}

		[Test]
		public void NumericTypesDowncast()
		{
			Test("42M as int", 42);
			Test("42M as long", 42L);
			Test("13.0M as double", 13.0);
			Test("13.0M as float", 13.0f);

			Test("13.37 as float", 13.37f);
			Test("13.37 as long", 13L);
			Test("13.37 as int", 13);

			Test("13.37f as long", 13L);
			Test("13.37f as int", 13);
		}

		[Test]
		public void NullableCast()
		{
			Test("null as int?", (int?)null);
			Test("1 as int?", (int?)1);
		}

		[Test]
		public void NullToObjectCast()
		{
			Test("null as string", null);
		}

		[Test]
		public void CastError1()
		{
			TestError("1.3 as string", CompilerMessages.CastTypesMismatch);
		}

		[Test]
		public void CastError2()
		{
			TestError("null as int", CompilerMessages.CastNullValueType);
		}

		[Test]
		public void Boxing()
		{
			Test("1 as object", 1);
			Test("42M as object", 42);
			Test("1.3 as object", 1.3);
			Test("true as object", true);
			Test("false as object", false);
		}

		[Test]
		public void Unboxing()
		{
//			Test("((1 as object) as int) == 1", true);
			Test("((42M as object) as decimal) == 42", true);
//			Test("(1.3 as object) as double", 1.3);
//			Test("(true as object) as bool", true);
//			Test("(false as object) as bool", false);
		}

		[Test]
		public void DelegateCasting()
		{
			var src = @"
var ts = (-> Console::WriteLine 1) as ThreadStart
ts ()";
			Test(src, null);
		}

		[Test]
		public void DelegateCasting2()
		{
			var src = @"
var filter = (x:int -> x > 2) as Predicate<int>
var arr = new [1; 2; 3; 4; 5]
Array::FindAll arr filter";

			Test(src, new[] { 3, 4, 5 });
		}

		[Test]
		public void DelegateCastingError1()
		{
			var src = @"
let fx = x:int -> x
fx as Func<string, int>
";
			TestError(src, CompilerMessages.CastDelegateArgTypesMismatch);
		}

		[Test]
		public void DelegateCastingError2()
		{
			var src = @"
let fx = x:int -> x
fx as Func<int, string>
";
			TestError(src, CompilerMessages.CastDelegateReturnTypesMismatch);
		}
	}
}