using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// Binding writes nothing into the parse tree: everything it learns lives in side tables owned
    /// by the context. These tests pin that rule down, because everything the language server will
    /// do rests on it.
    /// </summary>
    [TestFixture]
    internal class TreeImmutabilityTest : TestBase
    {
        private const string Script = @"
var data = new[1; 2; 3; 4; 5]
var acc = new List<int> ()
for x in data do
    if x % 2 == 0 then
        acc.Add (x * 2)
    else
        acc.Add (x * 3)

let sum = acc
  |> Sum ()
let doubler = (x:int) -> x * 2
$""{doubler sum}""";

        [Test]
        public void SameTreeCompilesTwiceWithTheSameResult()
        {
            var nodes = Parse(Script).ToArray();

            var first = Compile(nodes);
            var second = Compile(nodes);

            Assert.AreEqual("78", first);
            Assert.AreEqual(first, second);
        }

        [Test]
        public void CompilationDoesNotAlterTheTree()
        {
            var nodes = Parse(Script).ToArray();
            var pristine = Parse(Script).ToArray();

            Compile(nodes);

            // NodeBase compares structurally, so this asserts that the tree still describes the
            // same source it was parsed from - no node was replaced by its expansion
            Assert.AreEqual(pristine, nodes);
        }

        [Test]
        public void SameTreeCompilesTwiceEvenWithLambdaInference()
        {
            // an inferred lambda used to have its argument types written into it, and its cached
            // type cleared by hand, so a second compilation saw the leftovers of the first
            var nodes = Parse(@"
new[1; 2; 3]
  |> Select x -> x * 3
  |> Sum ()").ToArray();

            Assert.AreEqual(18, Compile(nodes));
            Assert.AreEqual(18, Compile(nodes));
        }

        [Test]
        public void FailedCompilationLeavesTheTreeIntact()
        {
            var nodes = Parse("1 + 2\nmissing\n3").ToArray();
            var pristine = Parse("1 + 2\nmissing\n3").ToArray();

            Assert.Throws<LensCompilerException>(() => Compile((IEnumerable<NodeBase>) nodes));

            Assert.AreEqual(pristine, nodes);
        }
    }
}
