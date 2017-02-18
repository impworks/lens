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

        /// <summary>
        /// https://github.com/impworks/lens/issues/182
        /// </summary>
        [Test]
        public void PatternMatchingBody()
        {
            var src = @"
match new [1; 2; 3] with
    case [x; ...y] then
        var t = y.GetType ()
        t.Name
";
            Test(src, "System.Int32[]");
        }
    }
}
