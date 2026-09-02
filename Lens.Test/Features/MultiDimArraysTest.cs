using System;
using System.Collections.Generic;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class MultiDimArraysTest : TestBase
    {
        #region Type signatures

        [Test]
        public void RankIsSpelledOutInATypeSignature()
        {
            Test(
                @"
var a : int[2d]
a = new int[2;3]
a[1;2] = 42
a[1;2]",
                42
            );
        }

        [Test]
        public void RankOneIsTheSameTypeAsAPlainVector()
        {
            Test(
                @"
var a : int[1d]
a = new [1; 2; 3]
a[1]",
                2
            );
        }

        [Test]
        public void RankIsPartOfTheTypeName()
        {
            TestType<int[,]>("typeof int[2d]");
            TestType<int[,,]>("typeof int[3d]");
            TestType<int[]>("typeof int[1d]");
            TestType<int[][,]>("typeof int[2d][]");
        }

        [Test]
        public void ARankMismatchIsNotAnImplicitConversion()
        {
            TestError(
                @"
var a : int[2d]
a = new [1; 2]",
                CompilerMessages.IdentifierTypeMismatch
            );
        }

        #endregion

        #region Creation

        [Test]
        public void AnEmptyArrayIsCreatedWithOneLengthPerDimension()
        {
            Test("(new int[2;3]).Length", 6);
            Test("(new int[2;3;4]).Length", 24);
            Test("(new int[2;3]).Rank", 2);
        }

        [Test]
        public void DimensionLengthsAreComputedAtRuntime()
        {
            Test(
                @"
var n = 3
var a = new int[n; n + 1]
a.GetLength 1",
                4
            );
        }

        [Test]
        public void AnEmptyArrayStartsOutFilledWithTheDefaultValue()
        {
            Test("(new string[2;2])[1;1]", null);
            Test("(new int[2;2])[1;1]", 0);
        }

        [Test]
        public void ALiteralIsWrittenAsNestedRows()
        {
            Test("(new @[[1; 2]; [3; 4]])[1;0]", 3);
            Test("(new @[[1; 2]; [3; 4]]).Length", 4);
            Test("(new @[[1; 2]; [3; 4]]).Rank", 2);
        }

        [Test]
        public void ALiteralOfRankThree()
        {
            Test("(new @[[[1; 2]; [3; 4]]; [[5; 6]; [7; 8]]])[1;0;1]", 6);
            Test("(new @[[[1; 2]; [3; 4]]; [[5; 6]; [7; 8]]]).Rank", 3);
        }

        [Test]
        public void ALiteralInfersItsElementType()
        {
            TestType<string[,]>(@"typeof string[2d]");
            Test(@"(new @[[""a""; ""b""]; [""c""; ""d""]])[0;1]", "b");
            Test("(new @[[1; 2]; [3; 4.5]])[0;0]", 1.0);
        }

        [Test]
        public void ALiteralAcceptsArbitraryExpressions()
        {
            Test(
                @"
var n = 10
var a = new @[[n; n * 2]; [n * 3; n * 4]]
a[1;1]",
                40
            );
        }

        [Test]
        public void ALiteralMayBeWrittenAcrossSeveralLines()
        {
            Test(
                @"
var a = new @[
    [1; 2]
    [
        3
        4
    ]
]
a[1;0] + a[0;1]",
                5
            );
        }

        [Test]
        public void ARaggedLiteralIsRejected()
        {
            TestError("new @[[1; 2]; [3; 4; 5]]", CompilerMessages.MultiDimArrayNotRectangular);
        }

        [Test]
        public void ALiteralWithUnevenNestingIsRejected()
        {
            TestError("new @[[1; 2]; 3]", CompilerMessages.MultiDimArrayRaggedNesting);
        }

        [Test]
        public void ALiteralOfUnrelatedItemsFallsBackToTheirCommonType()
        {
            Test(
                @"
var a = new @[[1; 2]; [3; new Exception ()]]
let t = a.GetType ()
t.ToString ()",
                "System.Object[,]"
            );
        }

        #endregion

        #region Access

        [Test]
        public void ReadingAndWritingGoThroughEveryDimension()
        {
            Test(
                @"
var a = new int[3;3]
for i in 0..3 do
    for j in 0..3 do
        a[i;j] = i * 3 + j

a[0;0] + a[1;1] + a[2;2]",
                12
            );
        }

        [Test]
        public void CompoundAssignmentReadsAndWritesTheSameCell()
        {
            Test(
                @"
var a = new @[[1; 2]; [3; 4]]
a[1;0] += 10
a[1;0]",
                13
            );
        }

        [Test]
        public void AnIndexListShorterThanTheRankIsRejected()
        {
            TestError(
                @"
var a = new int[2;3]
a[1]",
                CompilerMessages.ArrayRankMismatch
            );
        }

        [Test]
        public void AnIndexListLongerThanTheRankIsRejected()
        {
            TestError(
                @"
var a = new [1; 2; 3]
a[1;2]",
                CompilerMessages.ArrayRankMismatch
            );
        }

        [Test]
        public void AssigningThroughTheWrongNumberOfIndexesIsRejected()
        {
            TestError(
                @"
var a = new int[2;3]
a[1] = 5",
                CompilerMessages.ArrayRankMismatch
            );
        }

        [Test]
        public void LengthAndGetLengthAnswerTheWholeArrayAndOneDimension()
        {
            Test(
                @"
var a = new int[2;5]
a.Length * 100 + (a.GetLength 0) * 10 + (a.GetLength 1)",
                1025
            );
        }

        [Test]
        public void AMultiDimArrayIsPassedToAndReturnedFromAFunction()
        {
            Test(
                @"
fun sum:int (a:int[2d]) ->
    var acc = 0
    for i in 0..(a.GetLength 0) do
        for j in 0..(a.GetLength 1) do
            acc = acc + a[i;j]
    acc

sum (new @[[1; 2]; [3; 4]])",
                10
            );
        }

        [Test]
        public void AMultiDimArrayOfStructsIsWrittenThroughItsSetter()
        {
            Test(
                @"
var a = new System.DateTime[2;2]
a[0;1] = new System.DateTime 2020 1 1
a[0;1].Year",
                2020
            );
        }


        [Test]
        public void ACellIsPassedByReference()
        {
            Test(
                @"
fun bump (x:ref int) -> x = x + 10

var a = new @[[1; 2]; [3; 4]]
bump (ref a[1;0])
a[1;0]",
                13
            );
        }

        [Test]
        public void AStructCellIsMutatedInPlace()
        {
            Test(
                @"
var a = new System.Text.StringBuilder[1;1]
a[0;0] = new System.Text.StringBuilder ()
a[0;0].Append ""ab""
a[0;0].ToString ()",
                "ab"
            );
        }

        #endregion

        #region Iteration

        [Test]
        public void ForeachWalksEveryCellInRowMajorOrder()
        {
            Test(
                @"
var acc = """"
for x in new @[[1; 2]; [3; 4]] do
    acc = acc + x.ToString ()
acc",
                "1234"
            );
        }

        [Test]
        public void ForeachOverAMultiDimArrayBindsTheElementType()
        {
            Test(
                @"
var acc = 0
for x in new @[[1; 2]; [3; 4]] do
    acc = acc + x * 2
acc",
                20
            );
        }

        #endregion

        #region Rejected operations

        [Test]
        public void ConcatenationIsRejected()
        {
            TestError(
                @"
var a = new int[2;2]
a + a",
                CompilerMessages.MultiDimArrayOperator
            );
        }

        [Test]
        public void RepetitionIsRejected()
        {
            TestError(
                @"
var a = new int[2;2]
a * 2",
                CompilerMessages.MultiDimArrayOperator
            );
        }

        [Test]
        public void AnArrayPatternIsRejected()
        {
            TestError(
                @"
var a = new int[2;2]
match a with
    case [x; y] then 1
    case _ then 0",
                CompilerMessages.MultiDimArrayPattern
            );
        }


        [Test]
        public void AnArrayOfARecordTheScriptDeclaredIsCreatedAndRead()
        {
            Test(
                @"
record Point
    X : int
    Y : int

var a = new Point[2;2]
a[1;1] = new Point 3 4
a[1;1].X + a[1;1].Y",
                7
            );
        }

        [Test]
        public void ALiteralOfARecordTheScriptDeclared()
        {
            Test(
                @"
record Point
    X : int

var a = new @[[new Point 1; new Point 2]; [new Point 3; new Point 4]]
a[1;0].X",
                3
            );
        }

        [Test]
        public void ARecordArrayIsSpelledWithARank()
        {
            Test(
                @"
record Point
    X : int

var a : Point[2d]
a = new Point[1;1]
a[0;0] = new Point 9
a[0;0].X",
                9
            );
        }

        [Test]
        public void NullSafeIndexingCarriesEveryDimension()
        {
            Test(
                @"
var a : int[2d]
a?[0;0]",
                null
            );

            Test(
                @"
var a = new @[[1; 2]; [3; 4]]
a?[1;1]",
                4
            );
        }

        [Test]
        public void ARankIsSpelledInADeclareBlock()
        {
            TestConfigured(
                ctx => ctx.RegisterProperty("grid", () => new[,] {{1, 2}, {3, 4}}),
                @"
declare
    let grid : int[2d]

grid[1;0]",
                3
            );
        }

        #endregion

        #region Custom indexers

        [Test]
        public void AnIndexerOfSeveralArgumentsResolves()
        {
            TestConfigured(
                Setup(),
                @"
var m = new Matrix ()
m[1;2] = 42
m[1;2]",
                42
            );
        }

        [Test]
        public void OverloadsAreToldApartByTheirArgumentCount()
        {
            TestConfigured(Setup(), @"(new Matrix ())[3]", "one:3");
            TestConfigured(Setup(), @"(new Matrix ())[3;4]", 0);
            TestConfigured(Setup(), @"(new Matrix ())[""a""; ""b""; ""c""]", "three:abc");
        }

        [Test]
        public void AnIndexerIsPickedByTheDistanceOfEveryArgument()
        {
            TestConfigured(Setup(), @"(new Matrix ())[1; ""b""]", "mixed:1b");
        }

        [Test]
        public void AnIndexListNoOverloadAcceptsIsReported()
        {
            TestErrorConfigured(
                Setup(),
                @"(new Matrix ())[1; 2; 3; 4]",
                CompilerMessages.IndexGetterNotFound
            );
        }

        [Test]
        public void AnIndexerOnAGenericHostTypeTakesSeveralArguments()
        {
            TestConfigured(
                ctx => ctx.RegisterType("Grid", typeof(Grid<string>)),
                @"
var g = new Grid ()
g[1;1] = ""hit""
g[1;1]",
                "hit"
            );
        }

        #endregion

        #region Helpers

        private static Action<LensCompiler> Setup()
        {
            return ctx => ctx.RegisterType(typeof(Matrix));
        }

        private static void TestType<T>(string src)
        {
            Assert.AreEqual(typeof(T), Compile(src));
        }

        #endregion
    }

    public class Matrix
    {
        private readonly Dictionary<string, int> _cells = new Dictionary<string, int>();

        public int this[int x, int y]
        {
            get => _cells.TryGetValue(x + ":" + y, out var value) ? value : 0;
            set => _cells[x + ":" + y] = value;
        }

        public string this[int x] => "one:" + x;

        public string this[int x, string y] => "mixed:" + x + y;

        public string this[string a, string b, string c] => "three:" + a + b + c;
    }

    public class Grid<T>
    {
        private readonly Dictionary<string, T> _cells = new Dictionary<string, T>();

        public T this[int x, int y]
        {
            get => _cells.TryGetValue(x + ":" + y, out var value) ? value : default(T);
            set => _cells[x + ":" + y] = value;
        }
    }
}
