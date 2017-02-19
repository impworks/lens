using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// These tests contain descriptions of known problems which are not yet fixed.
    /// Corresponding Github issues are also mentioned.
    /// </summary>
    [Ignore]
    [TestFixture]
    internal class KnownIssues: TestBase
    {
        /// <summary>
        /// https://github.com/impworks/lens/issues/180
        /// </summary>
        [Test]
        public void ClosureAndOrdinaryAccess()
        {
            var src = @"
var x = 0
while x < 5 do
    (-> Console::WriteLine x) ()
    x += 1
";
            Test(src, null);
        }
    }
}
