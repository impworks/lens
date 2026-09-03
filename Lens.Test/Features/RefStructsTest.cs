using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// The two halves of ref struct support: a member that returns a managed pointer is readable
    /// and writable, and every use of a ref struct that the CLI forbids is reported while binding
    /// rather than by a verification failure at run time.
    /// </summary>
    [TestFixture]
    internal class RefStructsTest : TestBase
    {
        private const string Preamble = "use Lens.Test.Internals\n";

        #region By-ref returns

        [Test]
        public void ByRefReturnOfMethodIsRead()
        {
            Test(Preamble + @"
var a = new [1; 2; 3]
RefStructHost::FirstRef a", 1);
        }

        [Test]
        public void ByRefReturnFitsInALocal()
        {
            Test(Preamble + @"
var a = new [1; 2; 3]
var x = RefStructHost::FirstRef a
x + 1", 2);
        }

        [Test]
        public void ByRefReadonlyReturnIsRead()
        {
            Test(Preamble + @"
var a = new [4; 2; 3]
RefStructHost::FirstReadOnly a", 4);
        }

        [Test]
        public void ByRefReturnOfLargeStructIsRead()
        {
            Test(Preamble + @"
var a = new [new DateTime 2020 1 1]
(RefStructHost::FirstDate a).Year", 2020);
        }

        [Test]
        public void ByRefReturningIndexerIsRead()
        {
            Test(Preamble + @"
var a = new [1; 2; 3]
var w = RefStructHost::Window a
w[1]", 2);
        }

        [Test]
        public void ByRefReturningPropertyIsRead()
        {
            Test(Preamble + @"
var a = new [7; 2; 3]
var w = RefStructHost::Window a
w.First", 7);
        }

        [Test]
        public void ByRefReturningIndexerIsWritten()
        {
            Test(Preamble + @"
var a = new [1; 2; 3]
var w = RefStructHost::Window a
w[1] = 20
a[1]", 20);
        }

        [Test]
        public void ByRefReturningPropertyIsWritten()
        {
            Test(Preamble + @"
var a = new [1; 2; 3]
var w = RefStructHost::Window a
w.First = 10
a[0]", 10);
        }

        [Test]
        public void ByRefReturnIsPassedOnAsRefArgument()
        {
            Test(Preamble + @"
var a = new [1; 2; 3]
var w = RefStructHost::Window a
RefStructHost::Increment (ref w[1])
a[1]", 3);
        }

        #endregion

        #region Ref struct restrictions

        [Test]
        public void RefStructFlowsThroughAsAValue()
        {
            Test(Preamble + @"
var a = new [1; 2; 3]
let w = RefStructHost::Window a
RefStructHost::Total w", 6);
        }

        [Test]
        public void RefStructCannotBeBoxed()
        {
            TestError(Preamble + @"
var a = new [1; 2; 3]
(RefStructHost::Window a) as object", CompilerMessages.RefStructBoxed);
        }

        [Test]
        public void RefStructCannotBeAGenericArgument()
        {
            TestError(Preamble + @"
fun id<T>:T (x:T) -> x

var a = new [1; 2; 3]
id (RefStructHost::Window a)", CompilerMessages.RefStructGenericArgument);
        }

        [Test]
        public void RefStructCannotBeARecordField()
        {
            TestError(Preamble + @"
record Holder
    Data : IntWindow", CompilerMessages.RefStructField);
        }

        [Test]
        public void RefStructCannotBeCapturedByALambda()
        {
            TestError(Preamble + @"
var a = new [1; 2; 3]
let w = RefStructHost::Window a
let f = (x:int) -> w.Length
f 1", CompilerMessages.RefStructClosured);
        }

        [Test]
        public void RefStructCannotBeAnArrayElement()
        {
            TestError(Preamble + @"
var a = new [1; 2; 3]
let w = RefStructHost::Window a
new [w]", CompilerMessages.RefStructArrayElement);
        }

        [Test]
        public void RefStructCannotBeAnArrayElementBySignature()
        {
            TestError(Preamble + "let a = new IntWindow[2]", CompilerMessages.RefStructArrayElement);
        }

        [Test]
        public void RefStructCannotCrossASuspensionPoint()
        {
            TestError(Preamble + @"
fun windows:int~ ->
    var a = new [1; 2; 3]
    let w = RefStructHost::Window a
    yield w.Length
    yield w.Length

(windows()).ToArray()", CompilerMessages.RefStructInStateMachine);
        }

        [Test]
        public void RefStructCannotBeBoxedByAnInheritedCall()
        {
            // ToString is declared by ValueType and reached by boxing the receiver, which is the
            // one thing this receiver cannot do. Left undiagnosed, the runtime aborts the process
            // rather than throwing when the call is first made.
            TestError(Preamble + @"
var a = new [1; 2; 3]
(RefStructHost::Window a).ToString ()", CompilerMessages.RefStructBoxed);
        }

        [Test]
        public void RefStructCannotBeTheResultOfAScript()
        {
            // a script hands its result back as 'object'
            TestError(Preamble + @"
var a = new [1; 2; 3]
RefStructHost::Window a", CompilerMessages.ReturnTypeMismatch);
        }

        [Test]
        public void RefStructReachesItsOwnMembers()
        {
            Test(Preamble + @"
var a = new [1; 2; 3]
(RefStructHost::Window a).Sum ()", 6);
        }

        #endregion

        #region The BCL's own ref struct

#if NET_CORE

        [Test]
        public void SpanElementIsRead()
        {
            Test(Preamble + @"
var a = new [1; 2; 3]
var s = RefStructHost::Span a
s[2] + s.Length", 6);
        }

        [Test]
        public void SpanElementIsWritten()
        {
            Test(Preamble + @"
var a = new [1; 2; 3]
var s = RefStructHost::Span a
s[0] = 9
a[0]", 9);
        }

        [Test]
        public void ReadOnlySpanElementIsRead()
        {
            Test(Preamble + @"
var s = RefStructHost::Chars ""abc""
s[1]", 'b');
        }

        [Test]
        public void SpanIsPassedToAHostMethod()
        {
            Test(Preamble + @"
var a = new [1; 2; 3]
RefStructHost::SpanTotal (RefStructHost::Span a)", 6);
        }

        [Test]
        public void SpanToStringIsCalledDirectly()
        {
            // ReadOnlySpan overrides ToString, so the call needs no box and must still work
            Test(Preamble + @"
var s = RefStructHost::Chars ""abc""
s.ToString ()", "abc");
        }

        [Test]
        public void SpanCannotBeBoxed()
        {
            TestError(Preamble + @"
var a = new [1; 2; 3]
(RefStructHost::Span a) as object", CompilerMessages.RefStructBoxed);
        }

        [Test]
        public void SpanCannotBeAGenericArgument()
        {
            TestError(Preamble + @"
fun id<T>:T (x:T) -> x

var a = new [1; 2; 3]
id (RefStructHost::Span a)", CompilerMessages.RefStructGenericArgument);
        }

#endif

        #endregion
    }
}
