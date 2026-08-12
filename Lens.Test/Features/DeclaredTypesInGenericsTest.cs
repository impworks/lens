using System.Collections.Generic;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// Scripts that put a declared type inside an imported generic, in as many shapes as the
    /// language allows. This is the case that used to reach reflection through a
    /// NotSupportedException handler at every turn, because the CLR cannot answer questions about a
    /// constructed generic with a TypeBuilder in its argument tree.
    ///
    /// These compile and run, so they guard the behaviour while the member-lookup path is unified.
    /// </summary>
    [TestFixture]
    internal class DeclaredTypesInGenericsTest : TestBase
    {
        [Test]
        public void ListOfRecords()
        {
            Test(@"
record P
    X : int

var l = new List<P> ()
l.Add (new P 1)
l.Add (new P 2)
l.Count", 2);
        }

        [Test]
        public void ListOfRecordsIsEnumerable()
        {
            Test(@"
record P
    X : int

var l = new List<P> ()
l.Add (new P 3)
l.Add (new P 4)
l
  |> Select p -> p.X
  |> Sum ()", 7);
        }

        [Test]
        public void ArrayOfRecords()
        {
            Test(@"
record P
    X : int

var arr = new [
    new P 5
    new P 6
]
arr[1].X", 6);
        }

        [Test]
        public void DictionaryKeyedByRecord()
        {
            Test(@"
record P
    X : int

var d = new Dictionary<int, P> ()
d[1] = new P 9
d[1].X", 9);
        }

        [Test]
        public void MembershipTestOverADeclaredType()
        {
            // routes through EqualityComparer<P>.Default, which is an imported generic instantiated
            // over a declaration - one of the shapes that used to need a reflection fallback.
            //
            // Deliberately compares the same instance rather than two equal ones: a record's
            // generated Equals is not picked up by EqualityComparer<T>, so 'l.Contains (new P 1)'
            // answers false. That is pre-existing behaviour, unrelated to this work, and asserting
            // it here either way would be asserting something this test is not about.
            Test(@"
record P
    X : int

let p = new P 1
var l = new List<P> ()
l.Add p
l.Contains p", true);
        }

        [Test]
        public void LabelsOfAnAlgebraicTypeInAList()
        {
            Test(@"
type Shape
    Circle of int
    Empty

var l = new List<Shape> ()
l.Add (Circle 1)
l.Add Empty
l.Count", 2);
        }

        [Test]
        public void GenericRecordInstantiatedOverARecord()
        {
            Test(@"
record Inner
    X : int

record Box<T>
    Value : T

let b = new Box<Inner> (new Inner 7)
b.Value.X", 7);
        }

        [Test]
        public void NestedGenericsOverADeclaredType()
        {
            Test(@"
record P
    X : int

var outer = new List<List<P>> ()
var inner = new List<P> ()
inner.Add (new P 8)
outer.Add inner
outer[0][0].X", 8);
        }

        [Test]
        public void LambdaOverADeclaredTypeInAGeneric()
        {
            Test(@"
record P
    X : int

var l = new List<P> ()
l.Add (new P 10)
l
  |> Where p -> p.X > 5
  |> Select p -> p.X
  |> Sum ()", 10);
        }

        [Test]
        public void SortingRecordsByAProjection()
        {
            Test(@"
record P
    X : int

var l = new List<P> ()
l.Add (new P 3)
l.Add (new P 1)
l.Add (new P 2)
l
  |> OrderBy p -> p.X
  |> Select p -> p.X
  |> ToArray ()", new[] {1, 2, 3});
        }
    }
}
