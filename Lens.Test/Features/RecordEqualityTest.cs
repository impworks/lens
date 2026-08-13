using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// A record's generated Equals and GetHashCode override the ones it inherits, so anything that
    /// compares through the base type sees the fields rather than the identity.
    ///
    /// They used to be emitted with MethodAttributes.NewSlot, which takes a fresh vtable slot and
    /// leaves the inherited one alone - so the generated members were unreachable to every caller
    /// that did not know the exact static type, and a record was unusable as a dictionary key.
    /// </summary>
    [TestFixture]
    internal class RecordEqualityTest : TestBase
    {
        private const string Point = @"
record P
    X : int
    Y : int
";

        [Test]
        public void EqualRecordsCompareEqual()
        {
            Test(Point + "\n(new P 1 2).Equals (new P 1 2)", true);
        }

        [Test]
        public void DifferingRecordsCompareUnequal()
        {
            Test(Point + "\n(new P 1 2).Equals (new P 1 3)", false);
        }

        [Test]
        public void EqualityIsVisibleThroughObject()
        {
            // the case the NewSlot flag broke: the comparison goes through Object.Equals
            Test(Point + @"
let a = (new P 1 2) as object
a.Equals (new P 1 2)", true);
        }

        [Test]
        public void HashCodesAgreeWithEquality()
        {
            Test(Point + @"
let a = (new P 1 2) as object
let b = (new P 1 2) as object
a.GetHashCode () == b.GetHashCode ()", true);
        }

        [Test]
        public void DifferingRecordsHashDifferently()
        {
            Test(Point + @"
let a = (new P 1 2) as object
let b = (new P 9 9) as object
a.GetHashCode () == b.GetHashCode ()", false);
        }

        [Test]
        public void MembershipFindsAnEqualRecord()
        {
            Test(Point + @"
var l = new List<P> ()
l.Add (new P 1 2)
l.Contains (new P 1 2)", true);
        }

        [Test]
        public void ARecordWorksAsADictionaryKey()
        {
            Test(Point + @"
var d = new Dictionary<P, int> ()
d[new P 1 2] = 42
d[new P 1 2]", 42);
        }

        [Test]
        public void DistinctCollapsesEqualRecords()
        {
            Test(Point + @"
var l = new List<P> ()
l.Add (new P 1 2)
l.Add (new P 1 2)
l.Add (new P 3 4)
l
  |> Distinct ()
  |> Count ()", 2);
        }

        [Test]
        public void LabelsOfAnAlgebraicTypeCompareByValue()
        {
            Test(@"
type Shape
    Circle of int
    Empty

let a = (Circle 5) as object
a.Equals (Circle 5)", true);
        }

        [Test]
        public void LabelsOfDifferentKindsAreNotEqual()
        {
            Test(@"
type Shape
    Circle of int
    Square of int

let a = (Circle 5) as object
a.Equals (Square 5)", false);
        }

        [Test]
        public void AGenericRecordComparesByValue()
        {
            Test(@"
record Box<T>
    Value : T

let a = (new Box<int> 7) as object
a.Equals (new Box<int> 7)", true);
        }

        [Test]
        public void ARecordWithAReferenceFieldComparesByValue()
        {
            Test(@"
record Named
    Name : string
    Age : int

let a = (new Named ""bob"" 3) as object
a.Equals (new Named ""bob"" 3)", true);
        }

        [Test]
        public void ANestedRecordComparesByValue()
        {
            // the inner record's Equals is reached through EqualityComparer of the field's type,
            // which is the path that needed the override
            Test(@"
record Inner
    X : int

record Outer
    In : Inner

let a = (new Outer (new Inner 4)) as object
a.Equals (new Outer (new Inner 4))", true);
        }
    }
}
