using System.Collections.Generic;
using Lens.Compiler;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class IndexAndRangeTest : TestBase
    {
        #region Index

        [Test]
        public void IndexFromEndOfArray()
        {
            var src = @"
let a = new [1; 2; 3; 4; 5]
a[^2]
";
            Test(src, 4);
        }

        [Test]
        public void IndexFromEndOfString()
        {
            var src = @"
let s = ""hello""
s[^1]
";
            Test(src, 'o');
        }

        [Test]
        public void IndexFromEndOfList()
        {
            var src = @"
let a = new [[1; 2; 3]]
a[^1]
";
            Test(src, 3);
        }

        [Test]
        public void IndexFromEndOfExpression()
        {
            var src = @"
var calls = 0
let make = ->
    calls = calls + 1
    new [1; 2; 3]

let last = (make ())[^1]
new (last; calls)
";
            Test(src, new System.Tuple<int, int>(3, 1));
        }

        [Test]
        public void IndexFromEndOfComputedOffset()
        {
            var src = @"
let a = new [1; 2; 3; 4]
let k = 3
a[^(k - 1)]
";
            Test(src, 3);
        }

        [Test]
        public void IndexValueOfArray()
        {
            var src = @"
let a = new [1; 2; 3]
let idx = ^1
a[idx]
";
            Test(src, 3);
        }

        [Test]
        public void IndexValueBuiltByHand()
        {
            var src = @"
let a = new [1; 2; 3]
let idx = new System.Index 1 false
a[idx]
";
            Test(src, 2);
        }

        [Test]
        public void IndexFromEndAssignment()
        {
            var src = @"
let a = new [1; 2; 3]
a[^1] = 30
a[2]
";
            Test(src, 30);
        }

        [Test]
        public void IndexFromEndAssignmentToList()
        {
            var src = @"
let a = new [[1; 2; 3]]
a[^3] = 10
a[0]
";
            Test(src, 10);
        }

        [Test]
        public void IndexFromEndIsAnIndex()
        {
            var src = @"
let idx = ^1
idx.IsFromEnd
";
            Test(src, true);
        }

        [Test]
        public void IndexFromEndOfUncountableError()
        {
            var src = @"
let x = 5
x[^1]
";
            TestError(src, CompilerMessages.IndexFromEndNotSupported);
        }

        [Test]
        public void IndexFromEndWithoutIntegerIndexerError()
        {
            var src = @"
let d = new { ""a"" => 1 }
d[^1]
";
            TestError(src, CompilerMessages.IndexGetterNotFound);
        }

        [Test]
        public void IndexFromEndOfNonIntegerError()
        {
            var src = @"
let a = new [1; 2; 3]
a[^""x""]
";
            TestError(src, CompilerMessages.ImplicitCastImpossible);
        }

        [Test]
        public void IndexFromEndInMultiDimArrayError()
        {
            var src = @"
let a = new @[[1; 2]; [3; 4]]
a[^1; 0]
";
            TestError(src, CompilerMessages.IndexFromEndNotSupported);
        }

        #endregion

        #region Range

        [Test]
        public void RangeOfArray()
        {
            var src = @"
let a = new [1; 2; 3; 4; 5]
string::Join "","" (a[1..3])
";
            Test(src, "2,3");
        }

        [Test]
        public void RangeOfArrayWithoutStart()
        {
            var src = @"
let a = new [1; 2; 3; 4; 5]
string::Join "","" (a[..2])
";
            Test(src, "1,2");
        }

        [Test]
        public void RangeOfArrayWithoutEnd()
        {
            var src = @"
let a = new [1; 2; 3; 4; 5]
string::Join "","" (a[3..])
";
            Test(src, "4,5");
        }

        [Test]
        public void RangeOfArrayWithoutBounds()
        {
            var src = @"
let a = new [1; 2; 3]
(a[..]).Length
";
            Test(src, 3);
        }

        [Test]
        public void RangeOfArrayFromEnd()
        {
            var src = @"
let a = new [1; 2; 3; 4; 5]
string::Join "","" (a[^3..^1])
";
            Test(src, "3,4");
        }

        [Test]
        public void RangeOfArrayIsACopy()
        {
            var src = @"
let a = new [1; 2; 3]
let b = a[..]
b[0] = 100
a[0]
";
            Test(src, 1);
        }

        [Test]
        public void RangeOfString()
        {
            var src = @"
let s = ""hello""
s[1..^1]
";
            Test(src, "ell");
        }

        [Test]
        public void RangeOfList()
        {
            var src = @"
let a = new [[1; 2; 3; 4]]
string::Join "","" (a[1..3])
";
            Test(src, "2,3");
        }

        [Test]
        public void RangeValueOfArray()
        {
            var src = @"
let a = new [1; 2; 3; 4; 5]
let r = 1..3
string::Join "","" (a[r])
";
            Test(src, "2,3");
        }

        [Test]
        public void RangeValueIsARange()
        {
            var src = @"
let r = 1..^1
r.End.IsFromEnd
";
            Test(src, true);
        }

        [Test]
        public void RangeOfComputedBounds()
        {
            var src = @"
let a = new [1; 2; 3; 4; 5]
let k = 1
string::Join "","" (a[k + 1..k + 4])
";
            Test(src, "3,4,5");
        }

        [Test]
        public void RangeAssignmentToArray()
        {
            var src = @"
let a = new [1; 2; 3; 4]
a[1..3] = new [20; 30]
string::Join "","" a
";
            Test(src, "1,20,30,4");
        }

        [Test]
        public void RangeAssignmentToArrayOfWrongLength()
        {
            var src = @"
let a = new [1; 2; 3; 4]
var result = ""no error""
try
    a[1..3] = new [20]
catch ex:System.ArgumentException
    result = ""error""
result
";
            Test(src, "error");
        }

        [Test]
        public void RangeAssignmentToListOfSameLength()
        {
            var src = @"
let a = new [[1; 2; 3; 4]]
a[1..3] = new [20; 30]
string::Join "","" a
";
            Test(src, "1,20,30,4");
        }

        [Test]
        public void RangeAssignmentToListOfFewerValues()
        {
            var src = @"
let a = new [[1; 2; 3; 4]]
a[1..3] = new [20]
string::Join "","" a
";
            Test(src, "1,20,4");
        }

        [Test]
        public void RangeAssignmentToListOfMoreValues()
        {
            var src = @"
let a = new [[1; 2; 3; 4]]
a[1..3] = new [20; 30; 40]
string::Join "","" a
";
            Test(src, "1,20,30,40,4");
        }

        [Test]
        public void RangeAssignmentToStringError()
        {
            var src = @"
var s = ""hello""
s[1..3] = ""xx""
";
            TestError(src, CompilerMessages.RangeIndexNotSupported);
        }

        [Test]
        public void RangeOfDictionaryError()
        {
            var src = @"
let d = new { 1 => ""a"" }
d[1..2]
";
            TestError(src, CompilerMessages.RangeIndexNotSupported);
        }

        [Test]
        public void RangeOutsideOfBoundsError()
        {
            var src = @"
let a = new [1; 2; 3]
var result = ""no error""
try
    a[1..5]
catch ex:System.ArgumentOutOfRangeException
    result = ""error""
result
";
            Test(src, "error");
        }

        #endregion

        #region Iteration

        [Test]
        public void ForOverRangeValue()
        {
            var src = @"
var sum = 0
let r = 1..4
for i in r do
    sum = sum + i
sum
";
            Test(src, 6);
        }

        [Test]
        public void ForOverParenthesizedRange()
        {
            var src = @"
var sum = 0
for i in (2..5) do
    sum = sum + i
sum
";
            Test(src, 9);
        }

        [Test]
        public void ForOverRangeFromEndError()
        {
            var src = @"
let r = ..^1
var result = ""no error""
try
    for i in r do print i
catch ex:System.InvalidOperationException
    result = ""error""
result
";
            Test(src, "error");
        }

        [Test]
        public void ForOverRangeStaysAnIntegerLoop()
        {
            var src = @"
var sum = 0L
for i in 5L..1L do
    sum = sum + i
sum
";
            Test(src, 14L);
        }

        [Test]
        public void ForOverRangeInIterator()
        {
            var src = @"
fun walk:System.Collections.Generic.IEnumerable<int> (r:System.Range) ->
    for i in r do
        yield i

string::Join "","" (walk (1..4))
";
            Test(src, "1,2,3");
        }

        [Test]
        public void ForOverRangeWithoutEndError()
        {
            var src = @"
for i in 1.. do
    print i
";
            TestError(src, CompilerMessages.ForeachRangeNotStartBased);
        }

        [Test]
        public void ForOverRangeWithoutStartError()
        {
            var src = @"
for i in ..5 do
    print i
";
            TestError(src, CompilerMessages.ForeachRangeNotStartBased);
        }

        [Test]
        public void ForOverRangeFromEndBoundError()
        {
            var src = @"
let a = new [1; 2; 3]
for i in 1..^1 do
    print i
";
            TestError(src, CompilerMessages.ForeachRangeNotStartBased);
        }

        #endregion

        #region Safe mode

        /// <summary>
        /// The slicing helpers are the compiler's own, and a script that never named them is not
        /// refused for using what its own syntax lowers into.
        /// </summary>
        [Test]
        public void SlicingUnderWhitelist()
        {
            var opts = new LensCompilerOptions
            {
                SafeMode = SafeMode.Whitelist,
                SafeModeExplicitNamespaces = new List<string> {"System"}
            };

            var src = @"
let a = new [1; 2; 3; 4]
string::Join "","" (a[1..^1])
";

            Test(src, "2,3", opts);
        }

        #endregion
    }
}
