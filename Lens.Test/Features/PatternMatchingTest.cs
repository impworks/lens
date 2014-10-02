using NUnit.Framework;

namespace Lens.Test.Features
{
	[TestFixture]
	internal class PatternMatchingTest : TestBase
	{
		[Test]
		public void NumericLiterals()
		{
			var src = @"
match 2 with
    case 1 then ""one""
    case 2 then ""two""";

			Test(src, "two");
		}

		[Test]
		public void StringLiterals()
		{
			var src = @"
match ""one"" with
    case ""one"" then 1
    case ""two"" then 2";

			Test(src, 1);
		}

		[Test]
		public void BooleanLiterals()
		{
			var src = @"
match true with
    case true then ""true""
    case false then ""false""";

			Test(src, "true");
		}

		[Test]
		public void DefaultCatch()
		{
			var src = @"
match 2 with
    case 1 then ""one""
    case _ then ""other""";

			Test(src, "other");
		}

		[Test]
		public void MultipleCases()
		{
			var src = @"
match 1 with
    case 1 | 2 then ""two or less""
    case _ then ""other""";

			Test(src, "two or less");
		}
	}
}
