using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// Shorthand assignments to a target whose own type declares what they mean.
    /// </summary>
    [TestFixture]
    internal class CompoundAssignmentOperatorsTest : TestBase
    {
        #region Locals

        [Test]
        public void OperatorIsCalledOnALocal()
        {
            Test(@"
use Lens.Test.Internals
var bag = new Bag 10
bag += 5
bag.Total", 15);
        }

        [Test]
        public void EveryOperatorWithAMetadataNameIsFound()
        {
            Test(@"
use Lens.Test.Internals
var bag = new Bag 100
bag += 5
bag -= 3
bag *= 4
bag /= 2
bag %= 100
bag <:= 3
bag :>= 1
bag ^^= 6
bag &= 61
bag |= 2
bag.Total", (((((((((100 + 5) - 3) * 4) / 2) % 100) << 3) >> 1) ^ 6) & 61) | 2);
        }

        [Test]
        public void OperatorMutatesTheValueTypeInPlace()
        {
            Test(@"
use Lens.Test.Internals
var tally = new Tally ()
tally += 7
tally += 2
tally.Value", 9);
        }

        [Test]
        public void RightHandSideIsEvaluatedOnce()
        {
            Test(@"
use Lens.Test.Internals
var calls = 0
var bag = new Bag 0
let next = ->
    calls = calls + 1
    5
bag += next ()
new [calls; bag.Total]", new[] {1, 5});
        }

        #endregion

        #region Members and indexes

        [Test]
        public void OperatorIsCalledOnAMember()
        {
            Test(@"
use Lens.Test.Internals
var shelf = new Shelf ()
shelf.Slot += 4
shelf.Slot.Total", 4);
        }

        [Test]
        public void OperatorMutatesAValueTypeMemberInPlace()
        {
            Test(@"
use Lens.Test.Internals
var shelf = new Shelf ()
shelf.Count += 6
shelf.Count.Value", 6);
        }

        [Test]
        public void OperatorIsCalledOnAStaticMember()
        {
            Test(@"
use Lens.Test.Internals
Shelf::Shared = new Bag 1
Shelf::Shared += 2
Shelf::Shared.Total", 3);
        }

        [Test]
        public void MemberExpressionIsEvaluatedOnce()
        {
            Test(@"
use Lens.Test.Internals
var calls = 0
var shelf = new Shelf ()
let pick = ->
    calls = calls + 1
    shelf
(pick ()).Slot += 4
new [calls; shelf.Slot.Total]", new[] {1, 4});
        }

        [Test]
        public void OperatorIsCalledOnAnIndex()
        {
            Test(@"
use Lens.Test.Internals
var shelf = new Shelf ()
shelf.Row[1] += 8
new [shelf.Row[0].Total; shelf.Row[1].Total]", new[] {0, 8});
        }

        [Test]
        public void IndexExpressionIsEvaluatedOnce()
        {
            Test(@"
use Lens.Test.Internals
var calls = 0
var shelf = new Shelf ()
let pick = ->
    calls = calls + 1
    1
shelf.Row[pick ()] += 8
new [calls; shelf.Row[1].Total]", new[] {1, 8});
        }

        #endregion

        #region Fallbacks

        [Test]
        public void ClassicOperatorIsUsedWhenThereIsNoInPlaceOne()
        {
            Test(@"
use Lens.Test.Internals
var acc = new Accum 1
acc += 5
acc.Total", 6);
        }

        [Test]
        public void ValueReturningMethodIsNotAnOperator()
        {
            // 'Bag.op_MultiplicationAssignment' takes a string but returns a value, so '*=' with a
            // string must fall back to the read-modify-write and fail to find 'op_Multiply'
            TestError(@"
use Lens.Test.Internals
var bag = new Bag 1
bag *= ""nope""", CompilerMessages.OperatorBinaryTypesMismatch);
        }

        [Test]
        public void NumericShorthandIsUnaffected()
        {
            Test(@"
var x = 10
x += 5
x -= 3
x", 12);
        }

        /// <summary>
        /// '&amp;', '|' and '^' may precede the '=' of a shorthand just like the other binary
        /// operators, so each of them has to expand into the operator it is named after.
        /// </summary>
        [Test]
        public void BitwiseShorthandExpandsIntoItsOperator()
        {
            Test(@"
var x = 1337
x &= 42
x", 1337 & 42);

            Test(@"
var x = 1
x |= 6
x", 1 | 6);

            Test(@"
var x = 1337
x ^= 42
x", 1337 ^ 42);
        }

        [Test]
        public void BitwiseShorthandWorksOnAnIndex()
        {
            Test(@"
var arr = new [1337; 1]
arr[0] &= 42
arr[1] |= 6
new [arr[0]; arr[1]]", new[] {1337 & 42, 1 | 6});
        }

        [Test]
        public void BitwiseShorthandKeepsItsTypeError()
        {
            TestError(@"
var x = 1
x &= true", CompilerMessages.OperatorBinaryTypesMismatch);
        }

        [Test]
        public void EventSubscriptionIsUnaffected()
        {
            Test(@"
var count = 0
let obj = new Lens.Test.Features.EventSample ()
var handler = ((s e) -> count += 1) as System.EventHandler
obj.Basic += handler
obj.RaiseBasic ()
obj.Basic -= handler
obj.RaiseBasic ()
count", 1);
        }

        #endregion
    }
}
