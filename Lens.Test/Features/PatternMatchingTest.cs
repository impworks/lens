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

			Test(src, "one");
		}
	}
}
