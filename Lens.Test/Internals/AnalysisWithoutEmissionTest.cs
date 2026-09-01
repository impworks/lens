using System.Linq;
using Lens.Compiler;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Internals
{
    /// <summary>
    /// Analysis and emission are separate. A script can be read, bound and diagnosed without an
    /// assembly ever being defined - which is what a language server needs, since it re-analyses on
    /// every keystroke and cannot leak a dynamic module each time.
    /// </summary>
    [TestFixture]
    internal class AnalysisWithoutEmissionTest : TestBase
    {
        private static Context Analyze(string src)
        {
            var ctx = new Context(new LensCompilerOptions());
            ctx.Analyze(Parse(src).ToList());
            return ctx;
        }

        /// <summary>
        /// Asserts that analysis - which never emits - reported the given diagnostic.
        /// </summary>
        private static void AssertReports(Context ctx, string message)
        {
            var id = message.Substring(0, 6);

            Assert.IsTrue(
                ctx.Diagnostics.Any(d => d.Message.StartsWith(id)),
                "Expected {0}, got: {1}",
                message,
                string.Join(" | ", ctx.Diagnostics.Select(d => d.Message))
            );

            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void ConstructingAContextBuildsNoAssembly()
        {
            var ctx = new Context(new LensCompilerOptions());
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAnExpressionBuildsNoAssembly()
        {
            var ctx = Analyze("1 + 2 * 3");

            Assert.IsFalse(ctx.HasEmitTarget, "analysis must not define an assembly");
            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
        }

        [Test]
        public void AnalysingLocalsAndLambdasBuildsNoAssembly()
        {
            var ctx = Analyze(@"
var a = 1
let f = (x:int) -> x + a
f 2");

            Assert.IsFalse(ctx.HasEmitTarget);
            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
        }

        [Test]
        public void AnalysingARecordBuildsNoAssembly()
        {
            var ctx = Analyze(@"
record Point
    X : int
    Y : int

new Point 1 2");

            Assert.IsFalse(ctx.HasEmitTarget, "mentioning a record must not force its TypeBuilder");
            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
        }

        [Test]
        public void AnalysingAGenericFunctionBuildsNoAssembly()
        {
            var ctx = Analyze(@"
fun id<T>:T (x:T) -> x
id 5");

            Assert.IsFalse(ctx.HasEmitTarget);
            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
        }

        [Test]
        public void AnalysingAGenericOverADeclaredTypeBuildsNoAssembly()
        {
            var ctx = Analyze(@"
record P
    X : int

var l = new List<P> ()
l.Add (new P 1)
l");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty, "a declared type inside a generic must resolve");
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAnArrayOfATypeParameterBuildsNoAssembly()
        {
            var ctx = Analyze("fun first<T>:T (xs:T[]) -> xs[0]\nfirst (new[1; 2])");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAGenericRecordBuildsNoAssembly()
        {
            var ctx = Analyze("record B<T>\n    V : T\n\n1");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAGenericRecordWithACompositeFieldBuildsNoAssembly()
        {
            var ctx = Analyze("record B<T>\n    V : List<T>\n\n1");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAGenericAlgebraicTypeBuildsNoAssembly()
        {
            var ctx = Analyze("type Opt<T>\n    Some of T\n    None\n\n1");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysisReportsErrorsWithoutThrowing()
        {
            var ctx = Analyze("missing1\nmissing2");

            Assert.IsTrue(ctx.Diagnostics.HasErrors);
            Assert.AreEqual(2, ctx.Diagnostics.Count);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysisCanBeRepeatedOnTheSameTree()
        {
            var nodes = Parse("var x = 1\nlet y = x + 1\ny").ToList();

            var first = new Context(new LensCompilerOptions());
            first.Analyze(nodes);

            var second = new Context(new LensCompilerOptions());
            second.Analyze(nodes);

            Assert.IsTrue(first.Diagnostics.IsEmpty);
            Assert.IsTrue(second.Diagnostics.IsEmpty);
            Assert.IsFalse(first.HasEmitTarget);
            Assert.IsFalse(second.HasEmitTarget);
        }

        [Test]
        public void AnalysingAMatchBuildsNoAssembly()
        {
            // 'match' expands into jumps, and a jump label used to be reserved on the ILGenerator
            // while the tree was being bound - so a script containing one could not be analysed at
            // all, which is most of what a LENS script does
            var ctx = Analyze(@"
type Shape
    Empty
    Dot of int

fun describe:string (s:Shape) ->
    match s with
        case Empty then ""nothing""
        case Dot of n then ""a dot""

describe Empty");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAMatchProducingARecordBuildsNoAssembly()
        {
            // the fallback branch of a match that returns a value asks for the default of its type,
            // and materializing a declared type is what forces its builder into being
            var ctx = Analyze(@"
record Point
    X : int

fun pick:Point (flag:bool) ->
    match flag with
        case true then new Point 1
        case _ then new Point 2

pick true");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAMatchOverAGenericTypeInsideAGenericFunctionBuildsNoAssembly()
        {
            // the type a pattern names is the label instantiated with the arguments of the value
            // being matched - and both halves of that used to be built out of builders: the
            // declaration has none before emission, and neither has the T it is instantiated with
            var ctx = Analyze(@"
type Opt<T>
    None
    Some of T

fun map<T>:T (item:Opt<T> def:T) ->
    match item with
        case Some of x then x
        case None then def

map (Some 1) 0");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAFunctionThatReturnsNoValueReportsIt()
        {
            // the check used to live in the emission half only, so an editor - which never emits -
            // saw nothing wrong with a function whose body produces no value at all
            var ctx = Analyze(@"
fun add<T>:T (item:T arr:List<T>) ->
    arr.Add item

add 1 (new [[2]])");

            Assert.IsTrue(ctx.Diagnostics.HasErrors);
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAConstantPassedByReferenceReportsIt()
        {
            // the check used to live in the emission half only, so an editor - which never emits -
            // showed nothing for a 'let' handed to a 'ref' parameter
            var ctx = Analyze(@"
let y = 1
let x = int::TryParse ""x"" (ref y)");

            Assert.IsTrue(ctx.Diagnostics.HasErrors);
            Assert.IsTrue(ctx.Diagnostics.Any(d => d.Message == CompilerMessages.ConstantByRef));
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAConditionThatIsNotBooleanReportsIt()
        {
            var ctx = Analyze("if 1 then 2");

            AssertReports(ctx, CompilerMessages.ConditionTypeMismatch);
        }

        [Test]
        public void AnalysingAThrownNonExceptionReportsIt()
        {
            var ctx = Analyze("throw 1");

            AssertReports(ctx, CompilerMessages.ThrowTypeNotException);
        }

        [Test]
        public void AnalysingARethrowOutsideACatchClauseReportsIt()
        {
            var ctx = Analyze("throw");

            AssertReports(ctx, CompilerMessages.ThrowArgumentExpected);
        }

        [Test]
        public void AnalysingARethrowInsideACatchClauseReportsNothing()
        {
            // the clause a bare 'throw' belongs to used to be known only while emitting, and the
            // check above is worth nothing if it fires on the one case that is legal
            var ctx = Analyze(@"
try
    println ""a""
catch
    throw");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty);
        }

        [Test]
        public void AnalysingADictionaryKeyOfTheWrongTypeReportsIt()
        {
            var ctx = Analyze(@"new {1 => 2; ""a"" => 3}");

            AssertReports(ctx, CompilerMessages.DictionaryKeyTypeMismatch);
        }

        [Test]
        public void AnalysingAPropertyPassedByReferenceReportsIt()
        {
            var ctx = Analyze(@"
fun f (x:ref int) ->
    x = 1

var s = ""abc""
f (ref s.Length)");

            AssertReports(ctx, CompilerMessages.PropertyValuetypeRef);
        }

        [Test]
        public void AnalysingAnIndexerPassedByReferenceReportsIt()
        {
            var ctx = Analyze(@"
fun f (x:ref int) ->
    x = 1

var d = new {1 => 2}
f (ref d[1])");

            AssertReports(ctx, CompilerMessages.IndexerValuetypeRef);
        }

        [Test]
        public void AnalysingALambdaThatReturnsTheWrongTypeReportsIt()
        {
            // overload resolution takes a lambda for any delegate of the same arity, and the
            // signatures used to be reconciled only by the cast the emitter synthesizes
            var ctx = Analyze(@"
fun test:string (x:Func<int, int>) ->
    (x 1).ToString ()

test (a -> true)");

            AssertReports(ctx, CompilerMessages.CastDelegateReturnTypesMismatch);
        }

        [Test]
        public void AnalysingAnExtensionMethodOnAnArrayOfARecordBuildsNoAssembly()
        {
            // the extension method lookup used to be reflection over the referenced assemblies, so
            // a receiver that has no CLR type until the assembly exists could not be looked up at
            // all - and analysis reported the call as a method the type does not have, which is the
            // form the README's own sample takes
            var ctx = Analyze(@"
record Store
    Name : string
    Stock : int

let stores = new [
    new Store ""A"" 10
    new Store ""B"" 42
]

stores.OrderByDescending (x -> x.Stock)");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty, string.Join(" | ", ctx.Diagnostics.Select(d => d.Message)));
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAnExtensionMethodOnAListOfARecordBuildsNoAssembly()
        {
            var ctx = Analyze(@"
record Store
    Name : string
    Stock : int

let stores = new [[
    new Store ""A"" 10
    new Store ""B"" 42
]]

stores.OrderByDescending (x -> x.Stock)");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty, string.Join(" | ", ctx.Diagnostics.Select(d => d.Message)));
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingALambdaOverARecordBuildsNoAssembly()
        {
            // a lambda's own type is a Func over its arguments, and building that through
            // reflection is what used to materialize the record the lambda takes
            var ctx = Analyze(@"
record Store
    Name : string
    Stock : int

let f = (s:Store) -> s.Stock
f (new Store ""A"" 10)");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty, string.Join(" | ", ctx.Diagnostics.Select(d => d.Message)));
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void AnalysingAGenericCallInferredThroughALambdaOverARecordBuildsNoAssembly()
        {
            // TOutput is named nowhere but the lambda's body, so inferring it means typing the body
            // against the argument types the parameter supplies - in entry space, because the
            // receiver is a list of something that has no CLR type yet
            var ctx = Analyze(@"
record Store
    Name : string
    Stock : int

let stores = new [[new Store ""A"" 10]]
stores.ConvertAll (x -> x.Stock)");

            Assert.IsTrue(ctx.Diagnostics.IsEmpty, string.Join(" | ", ctx.Diagnostics.Select(d => d.Message)));
            Assert.IsFalse(ctx.HasEmitTarget);
        }

        [Test]
        public void CompilingStillBuildsAnAssembly()
        {
            var ctx = new Context(new LensCompilerOptions());
            ctx.Compile(Parse("1 + 2").ToList());

            Assert.IsTrue(ctx.HasEmitTarget);
        }

        [Test]
        public void AnExtensionMethodOnACollectionOfARecordStillRuns()
        {
            // the same call the two analysis tests above make, taken all the way through emission:
            // the lookup they share now hands out a recipe for the MethodInfo rather than the
            // MethodInfo itself, and this is what asks for it
            Test(
                @"
record Store
    Name : string
    Stock : int

let stores = new [[
    new Store ""A"" 10
    new Store ""B"" 42
    new Store ""C"" 5
]]

var names = """"
for s in stores.OrderByDescending (x -> x.Stock) do
    names = names + s.Name

names",
                "BAC"
            );
        }
    }
}
