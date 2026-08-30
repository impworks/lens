using System.Linq;
using Lens.Analysis;
using Lens.SyntaxTree;
using NUnit.Framework;

namespace Lens.Test.Internals
{
    /// <summary>
    /// The editor-facing view of a script: what it is made of, what went wrong with it, and where
    /// each name comes from. This is what a language server consumes, so it is checked here rather
    /// than through the server.
    /// </summary>
    [TestFixture]
    internal class ScriptAnalysisTest
    {
        #region Helpers

        private static ScriptAnalysis Analyze(string source)
        {
            return new ScriptAnalyzer().Analyze(source);
        }

        private static LexemLocation At(int line, int offset)
        {
            return new LexemLocation {Line = line, Offset = offset};
        }

        private static void AssertAll(TokenKind[] actual, TokenKind expected)
        {
            Assert.IsNotEmpty(actual);

            foreach (var curr in actual)
                Assert.AreEqual(expected, curr);
        }

        #endregion

        #region Diagnostics and tolerance

        [Test]
        public void EmptySourceIsAnalysedWithoutComplaint()
        {
            using (var analysis = Analyze(""))
            {
                Assert.IsEmpty(analysis.Diagnostics);
                Assert.IsEmpty(analysis.Tokens);
            }
        }

        [Test]
        public void CleanScriptReportsNothing()
        {
            using (var analysis = Analyze(@"
var a = 1
a + 2"))
            {
                Assert.IsEmpty(analysis.Diagnostics);
                Assert.IsFalse(analysis.HasSyntaxErrors);
            }
        }

        [Test]
        public void BindingErrorIsReportedWithALocation()
        {
            using (var analysis = Analyze("undefinedName + 1"))
            {
                Assert.AreEqual(1, analysis.Diagnostics.Count);
                Assert.IsTrue(analysis.Diagnostics[0].IsError);
                Assert.AreEqual(1, analysis.Diagnostics[0].Span.Start.Line);
            }
        }

        /// <summary>
        /// Analysis binds a script without building an assembly, so a declared generic function has
        /// no parameter builders and its type arguments are inferred from the declared signature
        /// instead. That inference only read a naked 'T' and 'T[]', so a signature naming List&lt;T&gt;
        /// left T unresolved - and the editor reported an error on a script that compiles and runs.
        /// </summary>
        [Test]
        public void AGenericArgumentIsInferredThroughACompositeSignature()
        {
            using (var analysis = Analyze(@"
fun firstOf<T>:T (items:List<T>) ->
    items[0]

firstOf (new [[""a""; ""b""; ""c""]])"))
            {
                Assert.IsEmpty(analysis.Diagnostics.Select(x => x.Message).ToArray());
            }
        }

        /// <summary>
        /// The type the call site passes need not name the definition the signature does: an
        /// argument declared as an interface is given something that implements it.
        /// </summary>
        [Test]
        public void AGenericArgumentIsInferredThroughAnImplementedInterface()
        {
            using (var analysis = Analyze(@"
use System.Collections.Generic

fun countOf<T>:int (items:IEnumerable<T>) -> 0

countOf (new [[1; 2; 3]])"))
            {
                Assert.IsEmpty(analysis.Diagnostics.Select(x => x.Message).ToArray());
            }
        }

        [Test]
        [TestCase("string::Fooo 1 2", TestName = "with arguments")]
        [TestCase("string::Fooo ()", TestName = "with none")]
        public void AMissingStaticMethodIsReportedWhereItIsCalled(string source)
        {
            // the lookup says 'not found' by throwing KeyNotFoundException, and letting that one
            // out reaches the editor as 'the given key was not present in the dictionary' pinned
            // to the first character of the file
            using (var analysis = Analyze(source))
            {
                var problem = analysis.Diagnostics.Single();

                StringAssert.Contains("Fooo", problem.Message);
                Assert.AreEqual(1, problem.Span.Start.Line);
                Assert.AreEqual(1, problem.Span.Start.Offset);
                Assert.Greater(problem.Span.End.Offset, 1);
            }
        }

        /// <summary>
        /// A function that awaits is rewritten into a state machine, and the awaited expression
        /// ends up inside a group of statements the script never contained. A mistake in it used
        /// to be followed by complaints about the awaiter the rewrite declares - a name with no
        /// location, which the editor pins to the first character of the file.
        /// </summary>
        [Test]
        public void ABrokenAwaitedExpressionIsReportedOnlyWhereItIsWritten()
        {
            using (var analysis = Analyze(@"
use System.Threading.Tasks
fun delay:Task (time:int) ->
    println ""before""
    await (Task::Delay ""x"")
    println ""after""

1"))
            {
                var problem = analysis.Diagnostics.Single();

                StringAssert.Contains("Delay", problem.Message);
                Assert.AreEqual(5, problem.Span.Start.Line);
            }
        }

        [Test]
        public void SeveralIndependentErrorsAreAllReported()
        {
            using (var analysis = Analyze(@"
missingOne ()
missingTwo ()"))
            {
                Assert.GreaterOrEqual(analysis.Diagnostics.Count, 2);
            }
        }

        [Test]
        public void AStatementThatDoesNotParseDoesNotStopTheRest()
        {
            using (var analysis = Analyze(@"
var a = 1
var = = =
var b = 2"))
            {
                Assert.IsTrue(analysis.HasSyntaxErrors);
                Assert.IsNotEmpty(analysis.Diagnostics);

                // the names on either side of the mistake are still known
                var names = analysis.Complete(At(4, 10)).Select(x => x.Label).ToArray();
                CollectionAssert.Contains(names, "a");
            }
        }

        [Test]
        public void ABrokenLineInsideAFunctionDoesNotLoseTheFunction()
        {
            using (var analysis = Analyze(@"
fun test:int ->
    var a = 1
    ???
    2

record Point
    X : int"))
            {
                Assert.IsTrue(analysis.HasSyntaxErrors);

                var outline = analysis.Outline.Select(x => x.Name).ToArray();
                CollectionAssert.Contains(outline, "test");
                CollectionAssert.Contains(outline, "Point");
            }
        }

        [Test]
        [TestCase("fun test ->\n    ", TestName = "an empty body, which is what an editor holds while the body is being typed")]
        [TestCase("fun test ->\n", TestName = "no body at all yet")]
        [TestCase("    ", TestName = "nothing but indentation")]
        [TestCase("record P\n    ", TestName = "a record whose first field has not been typed")]
        [TestCase("fun test ->\n        var a = 1\n  ", TestName = "a trailing line indented to no level in particular")]
        [TestCase("var a = \n", TestName = "an assignment with nothing to assign")]
        [TestCase("if true then\n", TestName = "a condition with no branch")]
        [TestCase("declare\n", TestName = "an empty declare block")]
        [TestCase("match x with\n", TestName = "a match with no cases")]
        [TestCase("\n\n\n", TestName = "nothing but blank lines")]
        public void AHalfTypedFileIsRead(string source)
        {
            // an editor asks about every intermediate state, and most of them are not valid LENS.
            // Whatever comes back, it must come back rather than throw.
            using (var analysis = Analyze(source))
            {
                Assert.DoesNotThrow(() => { var _ = analysis.Diagnostics; });
                Assert.DoesNotThrow(() => { var _ = analysis.Tokens; });
                Assert.DoesNotThrow(() => { var _ = analysis.Outline; });
                Assert.DoesNotThrow(() => analysis.Complete(At(1, 1)));
                Assert.DoesNotThrow(() => analysis.FindSymbol(At(1, 1)));
            }
        }

        [Test]
        public void ADedentToNoOpenLevelIsReportedRatherThanFatal()
        {
            // dedenting to a column no enclosing block is at used to pop the outermost level and
            // then read the empty stack, which is an InvalidOperationException rather than an error
            // anyone could act on
            using (var analysis = Analyze("var a = 1\nif true then\n        var b = 2\n    var c = 3"))
            {
                Assert.IsNotEmpty(analysis.Diagnostics);
                Assert.IsTrue(analysis.Diagnostics[0].Message.StartsWith("LE1"), analysis.Diagnostics[0].Message);
            }
        }

        [Test]
        public void ATrailingIndentedLineIsNotAnError()
        {
            // the caret sits here for as long as it takes to type the first character of the body,
            // and reporting an error under it the whole time is noise
            using (var analysis = Analyze("fun test:int ->\n    1\n    "))
            {
                Assert.IsEmpty(analysis.Diagnostics);
            }
        }

        [Test]
        public void AnUnclosedStringStillColoursWhatCameBefore()
        {
            using (var analysis = Analyze("var a = 1\nvar b = \"unclosed"))
            {
                Assert.IsNotEmpty(analysis.Diagnostics);
                CollectionAssert.Contains(analysis.Tokens.Select(x => x.Text).ToArray(), "a");
            }
        }

        [Test]
        public void ABadTypeConstraintIsReportedOnTheConstraint()
        {
            // signatures used to be resolved outside any recovery point, so a bad one escaped the
            // whole analysis and arrived as a failure with no location - which an editor draws over
            // the first character of the file, several lines from the mistake
            using (var analysis = Analyze("fun foo<T = string> ->\n    ()"))
            {
                Assert.AreEqual(1, analysis.Diagnostics.Count);

                var span = analysis.Diagnostics[0].Span;
                Assert.AreEqual(1, span.Start.Line);
                Assert.AreEqual(13, span.Start.Offset);
            }
        }

        [Test]
        public void ABadTypeConstraintNamingARecordIsReportedOnTheConstraint()
        {
            using (var analysis = Analyze("record Foo\n    X : int\n\nfun foo<T = Foo> ->\n    ()"))
            {
                Assert.AreEqual(1, analysis.Diagnostics.Count);
                Assert.AreEqual(4, analysis.Diagnostics[0].Span.Start.Line);
            }
        }

        [Test]
        public void AMissingMemberOfAGenericTypeIsReported()
        {
            // reaching a member of a List<T> used to materialize the type, and an analysis run has
            // emitted no builder for the T - which crashed the whole reading instead of reporting
            // that the member is not there
            using (var analysis = Analyze("fun add<T>:T (item:T arr:List<T>) ->\n    arr.Foo item\n    item"))
            {
                Assert.AreEqual(1, analysis.Diagnostics.Count);
                Assert.AreEqual(2, analysis.Diagnostics[0].Span.Start.Line);
                StringAssert.Contains("Foo", analysis.Diagnostics[0].Message);
            }
        }

        [Test]
        public void AValueOfTheWrongTypeIsRefusedByAFixedTypeParameter()
        {
            // overload resolution treats a generic parameter as an open slot, which it is for a
            // generic method and is not for the parameters an instantiation carries
            using (var analysis = Analyze("fun add<T>:T (item:T arr:List<T>) ->\n    arr.Add \"test\"\n    item"))
            {
                Assert.AreEqual(1, analysis.Diagnostics.Count);
                Assert.AreEqual(2, analysis.Diagnostics[0].Span.Start.Line);
            }
        }

        [Test]
        public void AGenericFunctionThatIsSoundReportsNothing()
        {
            using (var analysis = Analyze("fun add<T>:T (item:T arr:List<T>) ->\n    arr.Add item\n    item"))
            {
                Assert.IsEmpty(analysis.Diagnostics);
            }
        }

        #endregion

        #region Tokens

        [Test]
        public void PositionsCountFromOne()
        {
            using (var analysis = Analyze("var a = 1"))
            {
                var first = analysis.Tokens[0];

                Assert.AreEqual(TokenKind.Keyword, first.Kind);
                Assert.AreEqual(1, first.Span.Start.Line);
                Assert.AreEqual(1, first.Span.Start.Offset);
                Assert.AreEqual(4, first.Span.End.Offset);
            }
        }

        [Test]
        public void LiteralsAndKeywordsAreClassifiedLexically()
        {
            using (var analysis = Analyze("var a = \"text\"\nvar b = 42"))
            {
                var kinds = analysis.Tokens.ToDictionary(x => x.Span.Start.ToString(), x => x.Kind);

                // the string span covers its quotes, so it starts where the quote is
                Assert.AreEqual(TokenKind.String, kinds["1:9"]);
                Assert.AreEqual(TokenKind.Number, kinds["2:9"]);
            }
        }

        [Test]
        public void NamesAreClassifiedByWhatTheyTurnOutToBe()
        {
            using (var analysis = Analyze(@"
record Point
    X : int

fun scale:int (factor:int) -> factor * 2

var p = new Point 1
scale 2"))
            {
                var byText = analysis.Tokens
                                     .Where(x => x.Text != null)
                                     .GroupBy(x => x.Text)
                                     .ToDictionary(x => x.Key, x => x.Select(y => y.Kind).ToArray());

                AssertAll(byText["Point"], TokenKind.Type);
                AssertAll(byText["scale"], TokenKind.Function);
                AssertAll(byText["factor"], TokenKind.Parameter);
                AssertAll(byText["p"], TokenKind.Variable);
                AssertAll(byText["X"], TokenKind.Field);
            }
        }

        [Test]
        public void ReferenceIsAKeywordInsideADeclareBlock()
        {
            // the lexer has no such keyword - 'reference' is an ordinary identifier everywhere but
            // the one line it opens, and an editor colours what it is told rather than what it can
            // see for itself
            using (var analysis = Analyze("declare\n    reference \"System.Xml\""))
            {
                var word = analysis.Tokens.Single(x => x.Text == "reference");

                Assert.AreEqual(TokenKind.Keyword, word.Kind);
                Assert.AreEqual(2, word.Span.Start.Line);
                Assert.AreEqual(5, word.Span.Start.Offset);
            }
        }

        #endregion

        #region Symbols

        [Test]
        public void ALocalKnowsWhereItIsDeclaredAndUsed()
        {
            using (var analysis = Analyze(@"
var count = 1
count + count"))
            {
                var symbol = analysis.FindSymbol(At(3, 1));

                Assert.IsNotNull(symbol);
                Assert.AreEqual("count", symbol.Name);
                Assert.AreEqual(SymbolKind.Local, symbol.Kind);
                Assert.AreEqual(2, symbol.Declaration.Value.Start.Line);
                Assert.AreEqual(3, symbol.References.Count);
                Assert.IsTrue(symbol.CanRename);
            }
        }

        [Test]
        public void AnArgumentIsASymbolOfItsOwn()
        {
            using (var analysis = Analyze(@"
fun twice:int (value:int) -> value + value

twice 1"))
            {
                var symbol = analysis.FindSymbol(At(2, 31));

                Assert.IsNotNull(symbol);
                Assert.AreEqual("value", symbol.Name);
                Assert.AreEqual(SymbolKind.Parameter, symbol.Kind);
                Assert.AreEqual(3, symbol.References.Count);
            }
        }

        [Test]
        public void ALoopVariableIsTheSameSymbolInTheHeaderAndTheBody()
        {
            // the name a 'for' declares is not written by a declaration statement: the loop
            // expands into one, and the name the expansion declares has to be the same name the
            // body was bound against, or the header and the uses below it would be two symbols
            using (var analysis = Analyze(@"
for i in 1..10 do
    println (i + i)"))
            {
                var atDeclaration = analysis.FindSymbol(At(2, 5));
                var atUse = analysis.FindSymbol(At(3, 14));

                Assert.IsNotNull(atDeclaration);
                Assert.AreEqual(SymbolKind.Local, atDeclaration.Kind);
                Assert.AreEqual(2, atDeclaration.Declaration.Value.Start.Line);
                Assert.AreEqual(5, atDeclaration.Declaration.Value.Start.Offset);
                Assert.AreEqual(3, atDeclaration.References.Count);
                Assert.IsTrue(atDeclaration.CanRename);

                Assert.IsNotNull(atUse);
                Assert.AreEqual(atDeclaration.Declaration, atUse.Declaration);
                Assert.AreEqual(3, atUse.References.Count);
            }
        }

        [Test]
        public void ALoopVariableCapturedByALambdaIsStillOneSymbol()
        {
            using (var analysis = Analyze(@"
for i in 1..10 do
    let x = -> i
    println (x ())"))
            {
                var symbol = analysis.FindSymbol(At(3, 16));

                Assert.IsNotNull(symbol);
                Assert.AreEqual("i", symbol.Name);
                Assert.AreEqual(SymbolKind.Local, symbol.Kind);
                Assert.AreEqual(2, symbol.Declaration.Value.Start.Line);
                Assert.AreEqual(2, symbol.References.Count);
                Assert.IsTrue(symbol.CanRename);
            }
        }

        [Test]
        public void AFunctionIsFoundFromItsCallSite()
        {
            using (var analysis = Analyze(@"
fun answer:int -> 42

answer ()"))
            {
                var symbol = analysis.FindSymbol(At(4, 1));

                Assert.IsNotNull(symbol);
                Assert.AreEqual("answer", symbol.Name);
                Assert.AreEqual(SymbolKind.Function, symbol.Kind);
                Assert.AreEqual(2, symbol.Declaration.Value.Start.Line);
                Assert.AreEqual(5, symbol.Declaration.Value.Start.Offset);
                Assert.AreEqual(2, symbol.References.Count);
                Assert.IsTrue(symbol.CanRename);
            }
        }

        [Test]
        public void ARecordIsFoundFromItsUse()
        {
            using (var analysis = Analyze(@"
record Point
    X : int

new Point 1"))
            {
                var symbol = analysis.FindSymbol(At(5, 5));

                Assert.IsNotNull(symbol);
                Assert.AreEqual("Point", symbol.Name);
                Assert.AreEqual(SymbolKind.Record, symbol.Kind);
                Assert.AreEqual(2, symbol.References.Count);
            }
        }

        [Test]
        public void ALocalShadowingAFunctionIsNotMistakenForIt()
        {
            using (var analysis = Analyze(@"
fun size:int -> 1

fun other:int ->
    let size = 5
    size

size ()"))
            {
                var shadowed = analysis.FindSymbol(At(6, 5));
                Assert.AreEqual(SymbolKind.Local, shadowed.Kind);
                Assert.AreEqual(2, shadowed.References.Count);

                var function = analysis.FindSymbol(At(8, 1));
                Assert.AreEqual(SymbolKind.Function, function.Kind);

                // the two mentions inside 'other' belong to the local, not to the function
                Assert.AreEqual(2, function.References.Count);
            }
        }

        [Test]
        public void ANameInsideAnInterpolationHoleIsASymbol()
        {
            // the lexer hands the whole string over as one lexem, but the caret is on a name
            using (var analysis = Analyze(@"
let x = 1
print $""{x}"""))
            {
                var atUse = analysis.FindSymbol(At(3, 10));

                Assert.IsNotNull(atUse);
                Assert.AreEqual("x", atUse.Name);
                Assert.AreEqual(SymbolKind.Local, atUse.Kind);
                Assert.AreEqual(2, atUse.Declaration.Value.Start.Line);
                Assert.AreEqual(2, atUse.References.Count);
                Assert.IsTrue(atUse.CanRename);

                // the declaration site answers with the same symbol
                var atDeclaration = analysis.FindSymbol(At(2, 5));
                Assert.AreEqual(atUse.References.Count, atDeclaration.References.Count);
            }
        }

        [Test]
        public void APatternBindingIsASymbolOfItsOwn()
        {
            using (var analysis = Analyze(@"
match 1 with
    case n when n > 0 then n
    case _ then 0"))
            {
                var atUse = analysis.FindSymbol(At(3, 28));

                Assert.IsNotNull(atUse);
                Assert.AreEqual("n", atUse.Name);
                Assert.AreEqual(SymbolKind.Local, atUse.Kind);
                Assert.AreEqual(3, atUse.Declaration.Value.Start.Line);
                Assert.AreEqual(10, atUse.Declaration.Value.Start.Offset);
                Assert.AreEqual(3, atUse.References.Count);
                Assert.IsTrue(atUse.CanRename);

                // the pattern itself answers with the same symbol
                var atDeclaration = analysis.FindSymbol(At(3, 10));
                Assert.AreEqual(atUse.References.Count, atDeclaration.References.Count);
            }
        }

        [Test]
        public void ARegexGroupNameIsASymbolOfItsOwn()
        {
            // the lexer hands the whole literal over as one lexem, but the name of a group is a
            // variable the case declares, and nothing about it is different from 'case num:int'
            using (var analysis = Analyze(@"
match ""123"" with
    case #(?<num:int>[0-9]+)# then num
    case _ then 0"))
            {
                var atUse = analysis.FindSymbol(At(3, 36));

                Assert.IsNotNull(atUse);
                Assert.AreEqual("num", atUse.Name);
                Assert.AreEqual(SymbolKind.Local, atUse.Kind);
                Assert.AreEqual("var num : int", atUse.Detail);
                Assert.AreEqual(3, atUse.Declaration.Value.Start.Line);
                Assert.AreEqual(14, atUse.Declaration.Value.Start.Offset);
                Assert.AreEqual(17, atUse.Declaration.Value.End.Offset);
                Assert.AreEqual(2, atUse.References.Count);
                Assert.IsTrue(atUse.CanRename);

                // the name inside the literal answers with the same symbol
                var atDeclaration = analysis.FindSymbol(At(3, 14));
                Assert.AreEqual(atUse.References.Count, atDeclaration.References.Count);
                Assert.AreEqual(atUse.Declaration, atDeclaration.Declaration);
            }
        }

        [Test]
        public void ARegexGroupNameIsLocatedThroughTheEscapedDelimitersBeforeIt()
        {
            // '##' is how a literal spells one '#', so every column after it is shifted by one
            using (var analysis = Analyze(@"
match ""#1"" with
    case #a##(?<num:int>[0-9]+)# then num
    case _ then 0"))
            {
                var symbol = analysis.FindSymbol(At(3, 17));

                Assert.IsNotNull(symbol);
                Assert.AreEqual("num", symbol.Name);
                Assert.AreEqual(17, symbol.Declaration.Value.Start.Offset);
            }
        }

        [Test]
        public void ARegexGroupNameIsColouredAsAVariable()
        {
            using (var analysis = Analyze(@"
match ""123"" with
    case #(?<num:int>[0-9]+)# then num
    case _ then 0"))
            {
                var token = analysis.Tokens.Single(x => x.Span.Start.Line == 3 && x.Span.Start.Offset == 36);
                Assert.AreEqual(TokenKind.Variable, token.Kind);
            }
        }

        [Test]
        public void AGlobalUsedInsideAnInterpolationHoleIsAReference()
        {
            // renaming a function must not leave the mention inside a string behind
            using (var analysis = Analyze(@"
fun answer:int -> 42

print $""{answer ()}"""))
            {
                var symbol = analysis.FindSymbol(At(2, 5));

                Assert.IsNotNull(symbol);
                Assert.AreEqual(SymbolKind.Function, symbol.Kind);
                Assert.AreEqual(2, symbol.References.Count);
                Assert.AreEqual(4, symbol.References[1].Start.Line);
                Assert.AreEqual(10, symbol.References[1].Start.Offset);
            }
        }

        [Test]
        public void ARecordFieldIsFoundThroughItsReceiver()
        {
            using (var analysis = Analyze(@"
record Point
    X : int

fun readIt:int (p:Point) -> p.X"))
            {
                var atUse = analysis.FindSymbol(At(5, 31));

                Assert.IsNotNull(atUse);
                Assert.AreEqual(SymbolKind.RecordField, atUse.Kind);
                Assert.AreEqual(2, atUse.References.Count);
                Assert.IsTrue(atUse.CanRename);

                // the declaration site answers with the same symbol
                var atDeclaration = analysis.FindSymbol(At(3, 5));
                Assert.AreEqual(SymbolKind.RecordField, atDeclaration.Kind);
                Assert.AreEqual(2, atDeclaration.References.Count);
            }
        }

        [Test]
        public void FieldsOfDifferentRecordsAreNotConfused()
        {
            using (var analysis = Analyze(@"
record Left
    Value : int

record Right
    Value : int

fun readIt:int (l:Left r:Right) -> l.Value + r.Value"))
            {
                var symbol = analysis.FindSymbol(At(8, 38));

                Assert.AreEqual(SymbolKind.RecordField, symbol.Kind);

                // the declaration in Left and the one access through a Left, and nothing from Right
                Assert.AreEqual(2, symbol.References.Count);
                Assert.AreEqual(3, symbol.Declaration.Value.Start.Line);
            }
        }

        [Test]
        public void ADotNetMemberCannotBeRenamed()
        {
            using (var analysis = Analyze("var text = \"hello\"\nvar size = text.Length"))
            {
                var symbol = analysis.FindSymbol(At(2, 18));

                Assert.IsNotNull(symbol);
                Assert.AreEqual(SymbolKind.Member, symbol.Kind);
                Assert.IsFalse(symbol.CanRename);
            }
        }

        [Test]
        public void AHostNameCannotBeRenamed()
        {
            using (var analysis = Analyze("System.Math::Abs -1"))
            {
                var symbol = analysis.FindSymbol(At(1, 14));

                Assert.IsNotNull(symbol);
                Assert.IsFalse(symbol.CanRename);
                Assert.IsNotNull(symbol.RenameRefusal);
            }
        }

        [Test]
        public void AGenericTypeIsDescribedWithItsArguments()
        {
            // reflection names an instantiation after its definition - 'List`1' - and says nothing
            // about what it was instantiated with, which is the half a reader wants
            using (var analysis = Analyze("var x = new List<int> ()\nx"))
            {
                Assert.AreEqual("var x : List<int>", analysis.DescribeAt(At(1, 5)));
            }
        }

        [Test]
        public void ANestedGenericTypeIsDescribedWithItsArguments()
        {
            using (var analysis = Analyze("var x = new Dictionary<string, List<int>> ()\nx"))
            {
                Assert.AreEqual("var x : Dictionary<string, List<int>>", analysis.DescribeAt(At(1, 5)));
            }
        }

        [Test]
        public void EveryOverloadOfAMethodIsDescribed()
        {
            // the name on its own says nothing the line under the pointer does not already say:
            // what a reader wants is what the method takes, and which of the overloads they are on
            using (var analysis = Analyze("var text = \"hello\"\ntext.Substring(1)"))
            {
                var detail = analysis.DescribeAt(At(2, 7));

                StringAssert.Contains("Substring:string (startIndex:int)", detail);
                StringAssert.Contains("Substring:string (startIndex:int length:int)", detail);
            }
        }

        [Test]
        public void APropertyIsDescribedWithItsOwnerAndType()
        {
            using (var analysis = Analyze("var text = \"hello\"\ntext.Length"))
            {
                Assert.AreEqual("string.Length : int", analysis.DescribeAt(At(2, 7)));
            }
        }

        [Test]
        public void AMemberOfAnInstantiationIsDescribedWithItsArguments()
        {
            // reflection reports the members of List<int> in the terms of the definition they came
            // off, and a reader looking at a list of ints wants to be told 'int' rather than 'T'
            using (var analysis = Analyze("var xs = new List<int> ()\nxs.Add(1)"))
            {
                StringAssert.Contains("item:int", analysis.DescribeAt(At(2, 5)));
            }
        }

        [Test]
        public void EveryOverloadOfAStaticMethodIsDescribed()
        {
            using (var analysis = Analyze("var x = System.Math::Abs(-1)\nx"))
            {
                var detail = analysis.DescribeAt(At(1, 22));

                StringAssert.Contains("Abs:int (value:int)", detail);
                StringAssert.Contains("Abs:double (value:double)", detail);
            }
        }

        [Test]
        public void AnExtensionMethodIsDescribedWithItsSignature()
        {
            // nothing on int[] is called Where - the signature is the one the extension declares,
            // read in the terms of the receiver, and without the receiver itself: that goes to the
            // left of the dot rather than into the brackets
            using (var analysis = Analyze("var xs = new [1; 2; 3]\nxs.Where(x -> x > 1)"))
            {
                var detail = analysis.DescribeAt(At(2, 5));

                StringAssert.Contains("Where:IEnumerable<int> (predicate:Func<int, bool>)", detail);
                StringAssert.Contains("Where:IEnumerable<int> (predicate:Func<int, int, bool>)", detail);
                StringAssert.DoesNotContain("source:", detail);
            }
        }

        [Test]
        public void AMethodOfADeclaredRecordIsDescribed()
        {
            using (var analysis = Analyze(@"
record Point
    X : int

let p = new Point 1
p.Equals(p)"))
            {
                StringAssert.Contains("other:Point", analysis.DescribeAt(At(6, 3)));
            }
        }

        [Test]
        public void EveryOverloadOfAScriptFunctionIsDescribed()
        {
            using (var analysis = Analyze(@"
fun twice:int (value:int) -> value * 2
fun twice:string (value:string) -> value + value

twice 1"))
            {
                var detail = analysis.DescribeAt(At(5, 2));

                StringAssert.Contains("fun twice:int (value:int)", detail);
                StringAssert.Contains("fun twice:string (value:string)", detail);
            }
        }

        [Test]
        public void TheConstructorOfARecordIsDescribedByItsFields()
        {
            // the compiler's own constructor names its arguments '_x' and '_y', which is not a
            // spelling anybody wrote - what the reader needs is which field each one fills
            using (var analysis = Analyze(@"
record Point
    X : int
    Y : string

let p = new Point 1 ""a""
p"))
            {
                var detail = analysis.DescribeAt(At(6, 13));

                StringAssert.Contains("record Point", detail);
                StringAssert.Contains("new Point (X:int Y:string)", detail);
            }
        }

        [Test]
        public void TheConstructorOfAGenericRecordIsDescribedWithItsArguments()
        {
            using (var analysis = Analyze(@"
record Box<T>
    Value : T

let b = new Box<int> 1
b"))
            {
                StringAssert.Contains("new Box<int> (Value:int)", analysis.DescribeAt(At(5, 13)));
            }
        }

        [Test]
        public void EveryConstructorOfAHostTypeIsDescribed()
        {
            using (var analysis = Analyze("var xs = new List<int> ()\nxs"))
            {
                var detail = analysis.DescribeAt(At(1, 14));

                StringAssert.Contains("new List<int> ()", detail);
                StringAssert.Contains("new List<int> (capacity:int)", detail);
                StringAssert.Contains("new List<int> (collection:IEnumerable<int>)", detail);
            }
        }

        [Test]
        public void ConstructorsAreNotDescribedForAGenericArgumentOfTheTypeBeingBuilt()
        {
            // the 'int' of 'new List<int>' is a question about int, not about the list
            using (var analysis = Analyze("var xs = new List<int> ()\nxs"))
            {
                var detail = analysis.DescribeAt(At(1, 19));

                Assert.IsFalse(detail != null && detail.Contains("new List<int> (capacity:int)"));
            }
        }

        [Test]
        public void ConstructorsAreNotDescribedWhereNothingIsBeingConstructed()
        {
            using (var analysis = Analyze(@"
record Point
    X : int

fun shift:Point (p:Point) -> new Point (p.X + 1)"))
            {
                // the argument names the type, but it is not the one being built
                Assert.AreEqual("record Point", analysis.DescribeAt(At(5, 22)));
            }
        }

        [Test]
        public void ConstructorsAreDescribedWhileTheStatementStillDoesNotParse()
        {
            // 'new string' on its own is not a statement, and the editor asks about it anyway -
            // that is the moment somebody wants to be told what they could pass
            using (var analysis = Analyze("new string"))
            {
                Assert.IsTrue(analysis.HasSyntaxErrors);

                var detail = analysis.DescribeAt(At(1, 6));

                StringAssert.Contains("new string (c:char count:int)", detail);
                StringAssert.Contains("new string (value:char[])", detail);

                // a pointer is not something a script can hand over, and String takes four of them
                StringAssert.DoesNotContain("*", detail);
            }
        }

        [Test]
        public void ConstructorsAreDescribedWhenNoneOfThemMatchesTheArguments()
        {
            // the whole point of the tooltip here: the call binds to nothing, so the tree has no
            // type to offer, and the signature is written in the source all the same
            using (var analysis = Analyze("var x = new string 'x'\nx"))
            {
                Assert.IsNotEmpty(analysis.Diagnostics.Where(d => d.IsError));

                StringAssert.Contains("new string (c:char count:int)", analysis.DescribeAt(At(1, 14)));
            }
        }

        [Test]
        public void ConstructorsAreDescribedForADottedNameThatDoesNotParse()
        {
            using (var analysis = Analyze("new System.Text.StringBuilder"))
            {
                var detail = analysis.DescribeAt(At(1, 20));

                StringAssert.StartsWith("new StringBuilder ()", detail);
                StringAssert.Contains("new StringBuilder (capacity:int)", detail);
            }
        }

        [Test]
        public void ConstructorsAreDescribedForAGenericNameWithNoArgumentsWrittenYet()
        {
            // 'new List' names nothing on its own - the definition it is halfway to still says
            // what its constructors take, in the terms of its own parameters
            using (var analysis = Analyze("new List"))
            {
                var detail = analysis.DescribeAt(At(1, 6));

                StringAssert.Contains("new List<T> ()", detail);
                StringAssert.Contains("new List<T> (collection:IEnumerable<T>)", detail);
            }
        }

        [Test]
        public void ConstructorsAreDescribedForAGenericNameThatDoesNotParse()
        {
            using (var analysis = Analyze("new Dictionary<string, int>"))
            {
                var detail = analysis.DescribeAt(At(1, 6));

                StringAssert.Contains("new Dictionary<string, int> ()", detail);
                StringAssert.Contains("capacity:int", detail);
            }
        }

        [Test]
        public void ConstructorsAreDescribedForARecordThatDoesNotParse()
        {
            using (var analysis = Analyze(@"
record Point
    X : int
    Y : int

new Point"))
            {
                StringAssert.Contains("new Point (X:int Y:int)", analysis.DescribeAt(At(6, 6)));
            }
        }

        [Test]
        public void ConstructorsAreNotDescribedForAGenericArgumentOfAnUnparsedName()
        {
            using (var analysis = Analyze("new Dictionary<string, int>"))
            {
                var detail = analysis.DescribeAt(At(1, 17));

                Assert.IsFalse(detail != null && detail.Contains("new Dictionary"));
            }
        }

        [Test]
        public void TheLanguagesOwnTypeNamesAreOfferedAfterNew()
        {
            // 'string' is what a script writes, and no namespace leads to it - System holds a
            // 'String', which is a different word
            using (var analysis = Analyze("var x = new "))
            {
                var names = analysis.Complete(At(1, 13)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "string");
                CollectionAssert.Contains(names, "int");
            }
        }

        [Test]
        public void ADeclaredTypeAliasIsDescribedWithTheConstructorsItStandsFor()
        {
            using (var analysis = Analyze(@"
declare
    type Sb = System.Text.StringBuilder

let b = new Sb ()
b"))
            {
                var detail = analysis.DescribeAt(At(5, 13));

                StringAssert.Contains("type Sb = System.Text.StringBuilder", detail);
                StringAssert.Contains("new StringBuilder (capacity:int)", detail);
            }
        }

        [Test]
        public void RenameIsRefusedWhileTheFileDoesNotParse()
        {
            using (var analysis = Analyze(@"
var a = 1
var = = ="))
            {
                var symbol = analysis.FindSymbol(At(2, 5));

                Assert.IsNotNull(symbol);
                Assert.IsFalse(symbol.CanRename);
            }
        }

        #endregion

        #region Completion

        [Test]
        public void MembersAreOfferedAfterADot()
        {
            using (var analysis = Analyze("var text = \"hello\"\ntext."))
            {
                var names = analysis.Complete(At(2, 6)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Length");
                CollectionAssert.Contains(names, "Substring");
            }
        }

        [Test]
        public void ExtensionMethodsAreOffered()
        {
            using (var analysis = Analyze("var xs = new [1; 2; 3]\nxs."))
            {
                var names = analysis.Complete(At(2, 4)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Select");
                CollectionAssert.Contains(names, "Where");
            }
        }

        [Test]
        public void StaticMembersAreOfferedAfterDoubleColon()
        {
            using (var analysis = Analyze("string::"))
            {
                var names = analysis.Complete(At(1, 9)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Join");
                CollectionAssert.Contains(names, "IsNullOrEmpty");
                CollectionAssert.Contains(names, "Empty");

                // instance members are reached with '.', and offering them here offers what will
                // not compile
                CollectionAssert.DoesNotContain(names, "Substring");
            }
        }

        [Test]
        public void StaticMembersAreOfferedForANamespacedType()
        {
            using (var analysis = Analyze("System.Math::"))
            {
                var names = analysis.Complete(At(1, 14)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Abs");
                CollectionAssert.Contains(names, "PI");
            }
        }

        [Test]
        public void StaticMembersAreOfferedForATypeFromAnOpenNamespace()
        {
            using (var analysis = Analyze("use System.Linq\nEnumerable::"))
            {
                var names = analysis.Complete(At(2, 13)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Range");
                CollectionAssert.Contains(names, "Empty");
            }
        }

        [Test]
        public void StaticMembersAreOfferedAfterAPartialName()
        {
            using (var analysis = Analyze("string::Is"))
            {
                var names = analysis.Complete(At(1, 11)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "IsNullOrEmpty");
            }
        }

        [Test]
        public void StaticMembersAreOfferedForADeclaredTypeAlias()
        {
            using (var analysis = Analyze("declare\n    type Dt = System.DateTime\n\nDt::"))
            {
                var names = analysis.Complete(At(4, 5)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Now");
                CollectionAssert.Contains(names, "MaxValue");
            }
        }

        [Test]
        public void InstanceMembersAreNotOfferedForAName()
        {
            // a single ':' is a type annotation, not a member access
            using (var analysis = Analyze("var text = \"hi\"\nvar n:"))
            {
                var names = analysis.Complete(At(2, 7)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "text");
                CollectionAssert.DoesNotContain(names, "Substring");
            }
        }

        [Test]
        public void StaticMembersOfSomethingUnknownFallBackToNames()
        {
            using (var analysis = Analyze("var a = 1\nNoSuchType::"))
            {
                var names = analysis.Complete(At(2, 13)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "a");
            }
        }

        [Test]
        public void MembersAreOfferedAfterAPartialName()
        {
            using (var analysis = Analyze("var text = \"hello\"\ntext.Sub"))
            {
                var names = analysis.Complete(At(2, 9)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Substring");
            }
        }

        [Test]
        public void RecordFieldsAreOfferedAfterADot()
        {
            using (var analysis = Analyze(@"
record Point
    X : int
    Y : int

var p = new Point 1 2
p."))
            {
                var names = analysis.Complete(At(7, 3)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "X");
                CollectionAssert.Contains(names, "Y");
            }
        }

        [Test]
        public void FieldsOfAGenericRecordAreOfferedWithTheirActualTypes()
        {
            // an instantiation is not named after the declaration it was constructed from, so
            // looking the declaration up by name found nothing and no member was offered at all
            using (var analysis = Analyze(@"
record Foo<T = new>
    X : T

let y = new Foo<int> 1
y."))
            {
                var members = analysis.Complete(At(6, 3)).ToArray();
                var names = members.Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "X");

                // the X of Foo<int> is an int, whatever the declaration calls it
                Assert.AreEqual("int", members.First(x => x.Label == "X").Detail);
                StringAssert.Contains("other:Foo<int>", members.First(x => x.Label == "Equals").Detail);
            }
        }

        [Test]
        public void LabelFieldsOfAGenericAlgebraicTypeAreOffered()
        {
            using (var analysis = Analyze(@"
type Opt<T>
    None
    Some of T

let y = Some 1
y."))
            {
                var members = analysis.Complete(At(7, 3)).ToArray();

                CollectionAssert.Contains(members.Select(x => x.Label).ToArray(), "Tag");
                Assert.AreEqual("int", members.First(x => x.Label == "Tag").Detail);
            }
        }

        [Test]
        public void MembersAreOfferedOnTheRightOfAnAssignment()
        {
            // the assignment ends where its value does, and it is the wider of the two nodes -
            // but what is being completed on is the value, not the statement it is stored by
            using (var analysis = Analyze(@"
record Foo
    B : int

let x = new Foo 1
let z = x."))
            {
                var names = analysis.Complete(At(6, 11)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "B");
            }
        }

        [Test]
        public void MembersAreOfferedInsideAnExpressionThatContinuesAfterTheCaret()
        {
            // blanking the rest of the line would take the closing bracket with it, and a line
            // that does not parse binds nothing at all - the receiver included
            using (var analysis = Analyze(@"
record Foo
    B : int

let x = new Foo 1
let z = new [ x. ]"))
            {
                var names = analysis.Complete(At(6, 17)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "B");
                CollectionAssert.DoesNotContain(names, "print");
            }
        }

        [Test]
        public void MembersAreOfferedAfterAPipe()
        {
            // 'xs |> Select f' is 'xs.Select f', so a pipe completes exactly as a dot does - even
            // though what it applies to was written on an earlier line
            using (var analysis = Analyze(@"
let strings = new [
    ""foo""
    ""bar""
]

strings
    |> "))
            {
                var names = analysis.Complete(At(8, 8)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Select");
                CollectionAssert.Contains(names, "Where");
                CollectionAssert.Contains(names, "GetLength");
                CollectionAssert.DoesNotContain(names, "print");
            }
        }

        [Test]
        public void MembersAreOfferedAfterAPipeOnAPipedResult()
        {
            // what the second pipe applies to is the whole first call, and an invocation written
            // across lines does not end where its last argument does
            using (var analysis = Analyze(@"
let strings = new [
    ""foo""
    ""bar""
]

strings
    |> Where x -> x.Length > 2
    |> "))
            {
                var names = analysis.Complete(At(9, 8)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Select");
                CollectionAssert.DoesNotContain(names, "DynamicInvoke");
            }
        }

        [Test]
        public void MembersAreOfferedAfterAPartialNameFollowingAPipe()
        {
            using (var analysis = Analyze("let strings = new [\"foo\"]\nstrings\n    |> Sel"))
            {
                var names = analysis.Complete(At(3, 11)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Select");
            }
        }

        [Test]
        public void MembersAreOfferedOnALambdaArgumentInAPipedCall()
        {
            // an unparenthesised lambda ends exactly where its body does, so the widest expression
            // ending at the caret is the lambda itself - and its members are a delegate's
            using (var analysis = Analyze(@"
let items = new [
    ""foo""
    ""bar""
]

items
    |> Select x -> x."))
            {
                var names = analysis.Complete(At(8, 22)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Length");
                CollectionAssert.Contains(names, "Substring");
                CollectionAssert.DoesNotContain(names, "DynamicInvoke");
            }
        }

        [Test]
        public void MembersAreOfferedOnTheResultOfAParenthesisedCall()
        {
            // the other half of the same choice: a closing bracket ends a whole expression, and
            // the access applies to all of it rather than to the last thing inside it
            using (var analysis = Analyze("var x = \"hi\"\nx.Substring(1)."))
            {
                var names = analysis.Complete(At(2, 16)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Length");
            }
        }

        [Test]
        public void MembersAreOfferedOnAnArgumentRatherThanOnTheCall()
        {
            // 'print x.Length' prints a member of x - it does not read a member of the call
            using (var analysis = Analyze("var x = \"hi\"\nprint x."))
            {
                var names = analysis.Complete(At(2, 9)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Length");
            }
        }

        [Test]
        public void MembersAreOfferedInsideParentheses()
        {
            // '(x)' is parsed as the node 'x' with a span that covers the brackets, so nothing in
            // the tree ends where the receiver does
            using (var analysis = Analyze("var text = \"hi\"\nprint (text.Le)"))
            {
                var names = analysis.Complete(At(2, 15)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Length");
            }
        }

        [Test]
        public void AccessingAMissingRecordFieldIsReportedOnTheAccess()
        {
            // a declared type reports an unknown name by throwing, which used to escape binding as
            // a dictionary failure attributed to the first character of the file
            using (var analysis = Analyze(@"
record Foo
    B : int

let x = new Foo 1
let z = x.Bla
z"))
            {
                Assert.AreEqual(1, analysis.Diagnostics.Count(x => x.IsError && x.Message.Contains("Bla")));

                var diagnostic = analysis.Diagnostics.First(x => x.Message.Contains("Bla"));

                Assert.AreEqual(6, diagnostic.Span.Start.Line);
                Assert.AreEqual(9, diagnostic.Span.Start.Offset);
            }
        }

        [Test]
        public void AnErrorInsideACollectionIsReportedOnTheItem()
        {
            // a collection resolves its items itself, and used to claim every problem they have as
            // its own - highlighting the whole literal where one item is at fault
            using (var analysis = Analyze(@"
record Foo
    X : int

let f = new Foo 1
let fs = new [[ f.V ]]
fs"))
            {
                var diagnostic = analysis.Diagnostics.First(x => x.Message.Contains("'V'"));

                Assert.AreEqual(6, diagnostic.Span.Start.Line);
                Assert.AreEqual(17, diagnostic.Span.Start.Offset);
                Assert.AreEqual(20, diagnostic.Span.End.Offset);
            }
        }

        [Test]
        public void ACollectionWithoutACommonItemTypeIsReportedOnTheCollection()
        {
            // the other half of the same catch: a failure that belongs to no single item stays
            // with the literal, which is the only thing it can be about
            using (var analysis = Analyze("let xs = new [[ 1; print 1 ]]\nxs"))
            {
                var diagnostic = analysis.Diagnostics.First(x => x.IsError);

                Assert.AreEqual(1, diagnostic.Span.Start.Line);
                Assert.AreEqual(10, diagnostic.Span.Start.Offset);
                Assert.AreEqual(30, diagnostic.Span.End.Offset);
            }
        }

        [Test]
        public void VisibleNamesAreOfferedElsewhere()
        {
            using (var analysis = Analyze(@"
fun helper:int -> 1

var local = 2
"))
            {
                var names = analysis.Complete(At(5, 1)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "local");
                CollectionAssert.Contains(names, "helper");
                CollectionAssert.Contains(names, "match");
            }
        }

        [Test]
        public void MembersOfAGenericTypeAreOfferedInsideAGenericFunction()
        {
            // List<T> has no CLR type until the assembly is emitted, and an analysis run emits
            // nothing - so the members come off the definition, which carries the same names
            using (var analysis = Analyze("fun add<T>:T (item:T arr:List<T>) ->\n    arr.\n    item"))
            {
                var members = analysis.Complete(At(2, 9)).ToArray();
                var names = members.Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Add");
                CollectionAssert.Contains(names, "Count");

                StringAssert.Contains("item:T", members.First(x => x.Label == "Add").Detail);
            }
        }

        [Test]
        public void MembersOfAnInstantiationAreDescribedWithItsArguments()
        {
            using (var analysis = Analyze("var xs = new List<int> ()\nxs."))
            {
                var members = analysis.Complete(At(2, 4)).ToArray();

                StringAssert.Contains("item:int", members.First(x => x.Label == "Add").Detail);
            }
        }

        [Test]
        public void DeclaredEnvironmentIsOffered()
        {
            using (var analysis = Analyze(@"
declare
    let screen:string
    fun clamp:int (value:int)

"))
            {
                Assert.IsEmpty(analysis.Diagnostics);

                var names = analysis.Complete(At(5, 1)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "screen");
                CollectionAssert.Contains(names, "clamp");
            }
        }

        [Test]
        public void SubNamespacesAreOfferedInAUseDirective()
        {
            using (var analysis = Analyze("use System."))
            {
                var entries = analysis.Complete(At(1, 12)).ToArray();
                var names = entries.Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Collections");
                CollectionAssert.Contains(names, "Linq");
                CollectionAssert.Contains(names, "Text");

                // the segment alone is inserted, and the whole namespace is what it stands for
                Assert.AreEqual("System.Linq", entries.First(x => x.Label == "Linq").Detail);
                CollectionAssert.DoesNotContain(names, "System.Linq");

                // and it is a namespace that is being named, not a member of anything
                CollectionAssert.DoesNotContain(names, "String");
                CollectionAssert.DoesNotContain(names, "match");
            }
        }

        [Test]
        public void IntermediateNamespacesAreOfferedInAUseDirective()
        {
            // nothing is declared in System.Collections itself, and a list that cannot get there
            // cannot reach System.Collections.Generic either
            using (var analysis = Analyze("use System.Collections."))
            {
                var names = analysis.Complete(At(1, 24)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Generic");
            }
        }

        [Test]
        public void NamespaceRootsAreOfferedInAnEmptyUseDirective()
        {
            using (var analysis = Analyze("use "))
            {
                var names = analysis.Complete(At(1, 5)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "System");

                // a root is a whole segment, never a dotted path
                CollectionAssert.IsEmpty(names.Where(x => x.Contains(".")));
            }
        }

        [Test]
        public void PartiallyTypedNamespacesAreOfferedByTheirSegment()
        {
            // the editor filters the list by what has been typed, so the answer to 'System.Li' is
            // the same set of segments as the answer to 'System.'
            using (var analysis = Analyze("use System.Li"))
            {
                var names = analysis.Complete(At(1, 14)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "Linq");
            }
        }

        [Test]
        public void TypesAreOfferedAfterNew()
        {
            using (var analysis = Analyze("var x = new "))
            {
                var entries = analysis.Complete(At(1, 13)).ToArray();
                var names = entries.Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "List");
                CollectionAssert.Contains(names, "StringBuilder");

                // reflection names a definition 'List`1', which is not a name anybody can write
                Assert.AreEqual("System.Collections.Generic.List<T>", entries.First(x => x.Label == "List").Detail);
            }
        }

        [Test]
        public void TypesDeclaredByTheScriptAreOfferedAfterNew()
        {
            using (var analysis = Analyze(@"
record Point
    X : int
    Y : int

var p = new "))
            {
                var entries = analysis.Complete(At(6, 13)).ToArray();

                CollectionAssert.Contains(entries.Select(x => x.Label).ToArray(), "Point");
                Assert.AreEqual(SymbolKind.Record, entries.First(x => x.Label == "Point").Kind);
            }
        }

        [Test]
        public void NamesInScopeAreNotOfferedAfterNew()
        {
            // 'new' is followed by a type and by nothing else, and the names in scope would bury
            // the one word that could have compiled
            using (var analysis = Analyze("var total = 1\nvar x = new "))
            {
                var names = analysis.Complete(At(2, 13)).Select(x => x.Label).ToArray();

                CollectionAssert.DoesNotContain(names, "total");
                CollectionAssert.DoesNotContain(names, "print");
                CollectionAssert.DoesNotContain(names, "match");
            }
        }

        [Test]
        public void TypesAreOfferedAfterAPartialNameFollowingNew()
        {
            using (var analysis = Analyze("var x = new Str"))
            {
                var names = analysis.Complete(At(1, 16)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "StringBuilder");
            }
        }

        [Test]
        public void TypesThatCannotBeConstructedAreNotOfferedAfterNew()
        {
            using (var analysis = Analyze("var x = new "))
            {
                var names = analysis.Complete(At(1, 13)).Select(x => x.Label).ToArray();

                // an interface has no instances, and an abstract class has none of its own
                CollectionAssert.DoesNotContain(names, "IEnumerable");
                CollectionAssert.DoesNotContain(names, "Enumerable");
            }
        }

        [Test]
        public void NamespacesAreOfferedAfterNew()
        {
            // a type can be reached by spelling out where it lives, so the way there is part of
            // what may be written
            using (var analysis = Analyze("var x = new System.Text."))
            {
                var roots = analysis.Complete(At(1, 13)).Select(x => x.Label).ToArray();
                CollectionAssert.Contains(roots, "System");

                var names = analysis.Complete(At(1, 25)).Select(x => x.Label).ToArray();
                CollectionAssert.Contains(names, "StringBuilder");
                CollectionAssert.Contains(names, "RegularExpressions");

                // the namespace was named in full, so nothing outside it is on offer
                CollectionAssert.DoesNotContain(names, "List");
            }
        }

        [Test]
        [TestCase("declare\n    ", 2, 5, TestName = "nothing typed yet")]
        [TestCase("declare\n    re", 2, 7, TestName = "a word half typed")]
        [TestCase("declare // the environment\n    ", 2, 5, TestName = "a comment after the opening word")]
        [TestCase("declare\n    fun a:int\n\n    ", 4, 5, TestName = "a blank line between the entries")]
        public void OnlyDeclarationKeywordsAreOfferedInsideADeclareBlock(string source, int line, int offset)
        {
            using (var analysis = Analyze(source))
            {
                var entries = analysis.Complete(At(line, offset)).ToArray();

                CollectionAssert.AreEquivalent(
                    new[] {"reference", "type", "fun", "var", "let"},
                    entries.Select(x => x.Label).ToArray()
                );

                foreach (var curr in entries)
                    Assert.AreEqual(SymbolKind.Keyword, curr.Kind);
            }
        }

        [Test]
        public void NamesAreOfferedAgainOnceADeclarationHasBeenOpened()
        {
            // only the word that opens an entry is restricted: what follows it is a name, a type
            // and a signature like anywhere else
            using (var analysis = Analyze("declare\n    var x"))
            {
                var names = analysis.Complete(At(2, 10)).Select(x => x.Label).ToArray();

                CollectionAssert.DoesNotContain(names, "reference");
                Assert.Greater(names.Length, 5);
            }
        }

        [Test]
        public void ItemsAreOfferedInsideANewCollectionLiteral()
        {
            // 'new [' and 'new (' start a collection rather than name a type, and what goes in one
            // is an expression like any other
            using (var analysis = Analyze("var total = 1\nvar xs = new [ tot"))
            {
                var names = analysis.Complete(At(2, 19)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "total");
            }
        }

        [Test]
        public void NamesAreOfferedForTheArgumentsOfAConstructor()
        {
            using (var analysis = Analyze(@"
record Point
    X : int

var total = 1
var p = new Point tot"))
            {
                var names = analysis.Complete(At(6, 22)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "total");
            }
        }

        [Test]
        public void NamespacesAreNotOfferedOutsideAUseDirective()
        {
            using (var analysis = Analyze("var used = 1\nused"))
            {
                var names = analysis.Complete(At(2, 5)).Select(x => x.Label).ToArray();

                CollectionAssert.Contains(names, "used");
                CollectionAssert.DoesNotContain(names, "System");
            }
        }

        #endregion

        #region Outline

        [Test]
        public void OutlineListsWhatTheFileDeclares()
        {
            using (var analysis = Analyze(@"
record Point
    X : int
    Y : int

fun distance:int (p:Point) -> p.X + p.Y"))
            {
                Assert.AreEqual(2, analysis.Outline.Count);

                Assert.AreEqual("Point", analysis.Outline[0].Name);
                Assert.AreEqual(SymbolKind.Record, analysis.Outline[0].Kind);
                Assert.AreEqual(2, analysis.Outline[0].Children.Count);

                Assert.AreEqual("distance", analysis.Outline[1].Name);
                Assert.AreEqual(SymbolKind.Function, analysis.Outline[1].Kind);
            }
        }

        [Test]
        public void AnEmptyReferenceStillHasAnOutlineName()
        {
            // an editor refuses an outline entry with no name, and refuses the whole file's outline
            // along with it - so the half-typed 'reference ""' must not produce one
            using (var analysis = Analyze("declare\n    reference \"\""))
            {
                var entry = analysis.Outline.Single().Children.Single();

                Assert.IsNotEmpty(entry.Name);
            }
        }

        #endregion
    }
}
