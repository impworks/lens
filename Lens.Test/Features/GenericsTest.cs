using System;
using System.Collections.Generic;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class GenericsTest : TestBase
    {
        #region Generic functions

        [Test]
        public void FunctionWithInferredArgument()
        {
            var src = @"
fun id<T>:T (x:T) -> x
id 42";

            Test(src, 42);
        }

        [Test]
        public void FunctionWithSeveralParameters()
        {
            var src = @"
fun second<A, B>:B (a:A b:B) -> b
second 1 ""hello""";

            Test(src, "hello");
        }

        [Test]
        public void FunctionWithExplicitTypeArguments()
        {
            var src = @"
fun id<T>:T (x:T) -> x
id<object> 42";

            Test(src, 42);
        }

        [Test]
        public void FunctionUsedTwiceWithDifferentArguments()
        {
            var src = @"
fun id<T>:T (x:T) -> x
(id 1).ToString () + (id ""a"")";

            Test(src, "1a");
        }

        [Test]
        public void FunctionWithArrayArgument()
        {
            var src = @"
fun firstOf<T>:T (items:T[]) -> items[0]
firstOf (new [3; 4; 5])";

            Test(src, 3);
        }

        [Test]
        public void FunctionWithSequenceArgument()
        {
            var src = @"
fun count<T>:int (items:T~) -> items.Count ()
count (new [1; 2; 3])";

            Test(src, 3);
        }

        [Test]
        public void FunctionWithLambdaArgument()
        {
            var src = @"
fun apply<T, R>:R (x:T fx:Func<T, R>) -> fx x
apply 3 (a -> a * 2)";

            Test(src, 6);
        }

        [Test]
        public void GenericFunctionsAreNotConfused()
        {
            var src = @"
fun wrapA<T>:string (x:T) -> ""a"" + (x as object).ToString ()
fun wrapB<T>:string (x:T) -> ""b"" + (x as object).ToString ()
(wrapA 1) + (wrapB 2)";

            Test(src, "a1b2");
        }

        #endregion

        #region Constraints

        [Test]
        public void ConstraintInterfaceAllowsMemberAccess()
        {
            var src = @"
fun bigger<T = IComparable<T>>:T (a:T b:T) -> if (a.CompareTo b) > 0 then a else b
bigger 3 7";

            Test(src, 7);
        }

        [Test]
        public void ConstraintClassAcceptsReferenceType()
        {
            var src = @"
fun nullable<T = class>:bool (x:T) -> x == null
nullable ""a""";

            Test(src, false);
        }

        [Test]
        public void ConstraintNewCreatesInstance()
        {
            var src = @"
fun create<T = new>:T -> new T ()
(create<StringBuilder> ()).ToString ()";

            Test(src, "");
        }

        [Test]
        public void ConstraintsInArbitraryOrder()
        {
            var src = @"
fun test<T = new & class & IComparable>:string (x:T) -> x.ToString ()
test (new Version ())";

            Test(src, "0.0");
        }

        [Test]
        public void ConstraintNakedTypeParameterAsBase()
        {
            var src = @"
fun upcast<T, K = T>:T (x:K) -> x as T
(upcast<object, string> ""hi"") as string";

            Test(src, "hi");
        }

        [Test]
        public void ConstraintClassViolatedAtCallSite()
        {
            var src = @"
fun nullable<T = class>:bool (x:T) -> x == null
nullable 1";

            TestError(src, CompilerMessages.GenericClassConstraintViolated);
        }

        [Test]
        public void ConstraintStructViolatedAtCallSite()
        {
            var src = @"
fun test<T = struct>:string (x:T) -> x.ToString ()
test ""a""";

            TestError(src, CompilerMessages.GenericStructConstraintViolated);
        }

        [Test]
        public void ConstraintInterfaceViolatedAtCallSite()
        {
            var src = @"
fun test<T = IDisposable>:string (x:T) -> x.ToString ()
test 1";

            TestError(src, CompilerMessages.GenericInheritanceConstraintViolated);
        }

        [Test]
        public void ConstraintClassAndStructRejected()
        {
            var src = @"
fun test<T = class & struct>:string (x:T) -> ""a""
test 1";

            TestError(src, CompilerMessages.GenericConstraintClassAndStruct);
        }

        [Test]
        public void ConstraintStructAndNewRejected()
        {
            var src = @"
fun test<T = struct & new>:string (x:T) -> ""a""
test 1";

            TestError(src, CompilerMessages.GenericConstraintStructAndNew);
        }

        [Test]
        public void ConstraintDuplicateKeywordRejected()
        {
            var src = @"
fun test<T = class & class>:string (x:T) -> ""a""
test ""b""";

            TestError(src, CompilerMessages.GenericConstraintDuplicateKeyword);
        }

        [Test]
        public void ConstraintClassWithBaseTypeRejected()
        {
            var src = @"
fun test<T = class & Exception>:string (x:T) -> ""a""
test (new Exception ())";

            TestError(src, CompilerMessages.GenericConstraintBaseTypeAndKeyword);
        }

        [Test]
        public void ConstraintTwoBaseTypesRejected()
        {
            var src = @"
fun test<T = Exception & StringBuilder>:string (x:T) -> ""a""
test (new Exception ())";

            TestError(src, CompilerMessages.GenericConstraintMultipleBaseTypes);
        }

        [Test]
        public void ConstraintSealedBaseTypeRejected()
        {
            var src = @"
fun test<T = string>:string (x:T) -> ""a""
test ""b""";

            TestError(src, CompilerMessages.GenericConstraintInvalidBaseType);
        }

        [Test]
        public void ConstraintDuplicateInterfaceRejected()
        {
            var src = @"
fun test<T = IDisposable & IDisposable>:string (x:T) -> ""a""
test null";

            TestError(src, CompilerMessages.GenericConstraintDuplicateInterface);
        }

        [Test]
        public void ConstraintCircularRejected()
        {
            var src = @"
fun test<T = K, K = T>:string (a:T b:K) -> ""a""
test 1 2";

            TestError(src, CompilerMessages.GenericConstraintCircular);
        }

        [Test]
        public void TypeParameterRedefinitionRejected()
        {
            var src = @"
fun test<T, T>:string (a:T) -> ""a""
test 1";

            TestError(src, CompilerMessages.TypeParameterRedefinition);
        }

        #endregion

        #region Generic records

        [Test]
        public void RecordCreateAndRead()
        {
            var src = @"
record KeyValue<K, V>
    Key: K
    Value: V

let p = new KeyValue<string, int> ""x"" 1
p.Key + p.Value.ToString ()";

            Test(src, "x1");
        }

        [Test]
        public void RecordWithTypeParameterInField()
        {
            var src = @"
record Box<T>
    Items: T[]

let b = new Box<int> (new [1; 2; 3])
b.Items[1]";

            Test(src, 2);
        }

        [Test]
        public void RecordEqualityForReferenceTypes()
        {
            var src = @"
record Pair<A, B>
    First: A
    Second: B

let x = new Pair<string, string> ""a"" ""b""
let y = new Pair<string, string> ""a"" ""b""
x == y";

            Test(src, true);
        }

        [Test]
        public void RecordEqualityForValueTypes()
        {
            var src = @"
record Pair<A, B>
    First: A
    Second: B

let x = new Pair<int, int> 1 2
let y = new Pair<int, int> 1 2
let z = new Pair<int, int> 1 3
(x == y) && (x <> z)";

            Test(src, true);
        }

        [Test]
        public void RecordEqualityForNullables()
        {
            var src = @"
record Holder<T>
    Value: T

let x = new Holder<int?> 1
let y = new Holder<int?> 1
let z = new Holder<int?> null
(x == y) && (x <> z)";

            Test(src, true);
        }

        [Test]
        public void RecordHashCodeMatchesEquality()
        {
            var src = @"
record Pair<A, B>
    First: A
    Second: B

let x = new Pair<string, int> ""a"" 1
let y = new Pair<string, int> ""a"" 1
x.GetHashCode () == y.GetHashCode ()";

            Test(src, true);
        }

        [Test]
        public void RecordUsedAtSeveralInstantiations()
        {
            var src = @"
record Holder<T>
    Value: T

let a = new Holder<int> 1
let b = new Holder<string> ""z""
a.Value.ToString () + b.Value";

            Test(src, "1z");
        }

        [Test]
        public void RecordWrongTypeArgumentCount()
        {
            var src = @"
record Holder<T>
    Value: T

let a = new Holder<int, int> 1
a.Value";

            TestError(src, CompilerMessages.GenericTypeArgCountMismatch);
        }

        #endregion

        #region Generic algebraic types

        [Test]
        public void AlgebraicTaggedLabelInferred()
        {
            var src = @"
type Option<T>
    None
    Some of T

let x = Some 42
x.Tag";

            Test(src, 42);
        }

        [Test]
        public void AlgebraicTaggedLabelWithComplexTag()
        {
            var src = @"
type Foo<T>
    Bar
    Baz of Tuple<int, T>

let f = Baz (new (1; ""x""))
f.Tag.Item2";

            Test(src, "x");
        }

        [Test]
        public void AlgebraicUntaggedLabelWithExplicitTypeArguments()
        {
            var src = @"
type Option<T>
    None
    Some of T

let x = new None<int> ()
x <> null";

            Test(src, true);
        }

        [Test]
        public void AlgebraicMatchOnConstructedInstantiation()
        {
            var src = @"
type Option<T>
    None
    Some of T

fun describe<T>:string (x:Option<T>) ->
    match x with
        case Some of v then ""some "" + (v as object).ToString ()
        case None then ""none""

(describe (Some 1)) + "" / "" + (describe<string> (new None<string> ()))";

            Test(src, "some 1 / none");
        }

        [Test]
        public void AlgebraicLabelIsAssignableToParent()
        {
            var src = @"
type Option<T>
    None
    Some of T

fun unwrap<T>:T (x:Option<T> fallback:T) ->
    match x with
        case Some of v then v
        case None then fallback

unwrap (Some 5) 0";

            Test(src, 5);
        }

        #endregion

        #region Pattern matching on generic records

        [Test]
        public void MatchGenericRecord()
        {
            var src = @"
record Pair<A, B>
    First: A
    Second: B

let p = new Pair<string, int> ""a"" 2
match p with
    case Pair(First = f; Second = s) then f + s.ToString ()";

            Test(src, "a2");
        }

        #endregion

        #region Pure generic functions

        [Test]
        public void PureGenericFunctionMemoizesPerInstantiation()
        {
            var calls = 0;

            var src = @"
pure fun describe<T>:string (x:T) ->
    count ()
    (x as object).ToString ()

let a = describe 1
let b = describe 1
let c = describe ""z""
let d = describe ""z""
a + b + c + d";

            TestConfigured(
                cmp => cmp.RegisterFunction("count", (Action) (() => calls++)),
                src,
                "11zz"
            );

            // the body ran once per instantiation, not once per call
            Assert.AreEqual(2, calls);
        }

        #endregion

        #region Closures inside generic functions

        [Test]
        public void LambdaClosesOverTypeParameterLocal()
        {
            var src = @"
fun twice<T>:T (x:T) ->
    let fx = -> x
    fx ()

twice 7";

            Test(src, 7);
        }

        [Test]
        public void LambdaInsideLoopInsideGenericFunction()
        {
            var src = @"
fun collect<T>:IEnumerable<Func<T>> (x:T) ->
    var result = new List<Func<T>> ()
    for i in 1..4 do
        let local = x
        result.Add (-> local)
    result

var total = 0
for f in collect 5 do
    total = total + f ()

total";

            Test(src, 15);
        }

        #endregion

        #region Arity overloading

        [Test]
        public void TypeArityOverloadingRejected()
        {
            var src = @"
record Holder
    Value: int

record Holder<T>
    Value: T

1";

            TestError(src, CompilerMessages.TypeDefined);
        }

        [Test]
        public void FunctionArityOverloadingRejected()
        {
            var src = @"
fun test:string (x:int) -> ""a""
fun test<T>:string (x:T) -> ""b""
test 1";

            TestError(src, CompilerMessages.GenericArityOverloading);
        }

        #endregion

        #region The reference sample

        [Test]
        public void ReferenceSampleDeclarations()
        {
            // the declarations from plans/generics.lns, with a signature that can actually be
            // compiled: a 'pure' function must return a value, which 'doStuff' as written did not
            var src = @"
use System.Collections

pure fun doStuff<T = class & new & IEnumerable, K>:string (x:T) -> x.ToString ()

record KeyValue<K, V>
    Key: K
    Value: V

type Foo<T>
    Bar
    Baz of Tuple<int, T>

// 'K' appears nowhere in the signature, so it cannot be inferred and has to be given
let s = doStuff<ArrayList, int> (new ArrayList ())
let p = new KeyValue<string, int> ""x"" 1
let f = Baz (new (1; ""x""))

p.Key + f.Tag.Item2 + s";

            Test(src, "xxSystem.Collections.ArrayList");
        }

        #endregion

        #region Untagged labels

        [Test]
        public void UntaggedGenericLabelWithoutTypeArgumentsRejected()
        {
            var src = @"
type Option<T>
    None
    Some of T

let x = None
x";

            TestError(src, CompilerMessages.GenericLabelTypeArgsRequired);
        }

        [Test]
        public void UntaggedGenericLabelWithTypeArguments()
        {
            var src = @"
type Option<T>
    None
    Some of T

let x = None<int>
x <> null";

            Test(src, true);
        }

        [Test]
        public void UntaggedGenericLabelWithTypeArgumentsIsProperlyTyped()
        {
            var src = @"
type Option<T>
    None
    Some of T

fun describe:string (x:Option<int>) ->
    match x with
        case Some of v then ""some "" + v.ToString ()
        case None then ""none""

(describe None<int>) + "" / "" + (describe (Some 1))";

            Test(src, "none / some 1");
        }

        [Test]
        public void UntaggedGenericLabelWithWrongTypeArgumentCount()
        {
            var src = @"
type Option<T>
    None
    Some of T

let x = None<int, string>
x";

            TestError(src, CompilerMessages.GenericTypeArgCountMismatch);
        }

        [Test]
        public void UntaggedNonGenericLabelWithTypeArguments()
        {
            var src = @"
type Color
    Red
    Green

let x = Red<int>
x";

            TestError(src, CompilerMessages.GenericTypeArgCountMismatch);
        }

        #endregion
    }
}
