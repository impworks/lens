using System.Linq;
using Lens.Compiler;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// Every declaration gets one object with an identity, a declaration site and the list of
    /// places that name it. Without that list, a rename is a text search.
    /// </summary>
    [TestFixture]
    internal class SymbolsTest : TestBase
    {
        private static Context Bind(string src)
        {
            var ctx = new Context(new LensCompilerOptions());
            ctx.Compile(Parse(src));
            return ctx;
        }

        private static Local Symbol(Context ctx, string name)
        {
            var matches = ctx.LocalSymbols.Where(x => x.Name == name).ToArray();
            Assert.AreEqual(1, matches.Length, "Expected exactly one symbol named '{0}'", name);
            return matches[0];
        }

        [Test]
        public void LookupReturnsTheDeclarationItselfNotACopy()
        {
            var ctx = Bind("var x = 1\nx = x + 1\nx");

            var declared = Symbol(ctx, "x");
            Assert.AreSame(declared, ctx.ScopeOf(ctx.MainMethod.Body).FindLocal("x"));
        }

        [Test]
        public void ADeclarationKnowsWhereItIs()
        {
            var ctx = Bind("var x = 1\nx");

            var x = Symbol(ctx, "x");
            Assert.IsNotNull(x.Declaration);
            Assert.AreEqual(1, x.Declaration.StartLocation.Line);
        }

        [Test]
        public void ADeclarationKnowsEveryPlaceThatNamesIt()
        {
            // 'x' is read on line 2, assigned on line 3, read again on line 4
            var ctx = Bind(@"
var x = 1
let y = x + 1
x = 5
x");

            var x = Symbol(ctx, "x");
            var lines = x.References.Select(r => r.StartLocation.Line).OrderBy(l => l).ToArray();

            Assert.AreEqual(new[] {3, 4, 5}, lines);
        }

        [Test]
        public void AnArgumentIsADeclarationToo()
        {
            var ctx = Bind(@"
fun twice:int (n:int) -> n + n
twice 3");

            var n = Symbol(ctx, "n");
            Assert.IsNotNull(n.Declaration);
            Assert.AreEqual(2, n.References.Count);
        }

        [Test]
        public void TwoVariablesOfTheSameNameInSiblingScopesAreTwoSymbols()
        {
            var ctx = Bind(@"
if true then
    let p = 1
    p
else
    let p = 2
    p
0");

            var symbols = ctx.LocalSymbols.Where(x => x.Name == "p").ToArray();
            Assert.AreEqual(2, symbols.Length);
            Assert.AreNotSame(symbols[0], symbols[1]);
            Assert.AreNotEqual(symbols[0].Declaration.StartLocation.Line, symbols[1].Declaration.StartLocation.Line);
        }
    }
}
