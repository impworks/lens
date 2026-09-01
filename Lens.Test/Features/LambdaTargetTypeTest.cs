using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// A lambda literal is not a delegate of any particular family until something says which.
    ///
    /// The same expression reaches a Func, a Converter and a Predicate, and which of them it becomes
    /// is decided by the parameter it is passed to, the location it is assigned to or the type it is
    /// cast to - not by the literal itself. Where nothing says, it settles into the Func or Action
    /// its own signature describes.
    /// </summary>
    [TestFixture]
    internal class LambdaTargetTypeTest : TestBase
    {
        #region Delegate families other than Func and Action

        [Test]
        public void TypedLambdaReachesAConverter()
        {
            Test(@"
let l = new [[1; 2; 3]]
l.ConvertAll ((x:int) -> x * 2)
    |> Sum ()",
                12
            );
        }

        [Test]
        public void UntypedLambdaReachesAConverter()
        {
            Test(@"
let l = new [[1; 2; 3]]
l.ConvertAll (x -> x * 2)
    |> Sum ()",
                12
            );
        }

        [Test]
        public void TypedLambdaReachesAPredicate()
        {
            Test(@"
let l = new [[1; 2; 3; 4]]
(l.FindAll ((x:int) -> x > 2)).Count",
                2
            );
        }

        [Test]
        public void TypedLambdaReachesAComparison()
        {
            Test(@"
let l = new [[3; 1; 2]]
l.Sort ((a:int b:int) -> b - a)
l[0]",
                3
            );
        }

        #endregion

        #region The delegate is built directly

        [Test]
        public void TheTargetDelegateIsBuiltWithoutAnIntermediateFunc()
        {
            // the whole point of deciding the delegate before emission: building the Func the
            // literal describes and converting it afterwards allocates two delegates and calls the
            // body through both of them
            Test(@"Lens.Test.Internals.DelegateShape::NameOf ((x:int) -> x * 2)", "Converter`2");

            Test(@"Lens.Test.Internals.DelegateShape::IsDirectConverter ((x:int) -> x * 2)", true);
            Test(@"Lens.Test.Internals.DelegateShape::IsDirectConverter (x -> x * 2)", true);
            Test(@"Lens.Test.Internals.DelegateShape::IsDirectPredicate ((x:int) -> x > 1)", true);
        }

        [Test]
        public void ACastBuildsTheTargetDelegateDirectly()
        {
            Test(@"
let c = ((x:int) -> x * 2) as Converter<int, int>
Lens.Test.Internals.DelegateShape::IsDirectConverter c",
                true
            );
        }

        #endregion

        #region Settling where nothing names a delegate

        [Test]
        public void ALocalDeclarationSettlesOnFunc()
        {
            Test(@"
let f = (x:int) -> x * 2
(f.GetType ()).Name",
                "Func`2"
            );
        }

        [Test]
        public void ALocalDeclarationSettlesOnActionWhenNothingIsReturned()
        {
            Test(@"
let f = (x:int) -> print """"
(f.GetType ()).Name",
                "Action`1"
            );
        }

        [Test]
        public void ALocalDeclarationOfAnUntypedLambdaIsRefused()
        {
            TestError("let f = x -> x * 2\nf 1", CompilerMessages.LambdaArgTypeUnknown);
        }

        [Test]
        public void ACollectionSettlesItsItemsOnFunc()
        {
            Test(@"
let fs = new [((x:int) -> x * 2)]
(fs[0]) 21",
                42
            );
        }

        [Test]
        public void ALambdaInvokedOnTheSpotSettlesOnFunc()
        {
            Test(@"((x:int) -> x * 2) 21", 42);
        }

        [Test]
        public void ALambdaPassedWhereNoDelegateIsWantedSettlesOnFunc()
        {
            // 'object' names no delegate for the literal to become, so it arrives as the Func its
            // own signature describes - and as an ordinary reference, which is what the parameter
            // is asking for
            Test(@"((((x:int) -> x * 2) as object).GetType ()).Name", "Func`2");
        }

        [Test]
        public void ALambdaAssignedToAFieldBecomesThatFieldsDelegate()
        {
            Test(@"
var holder = new Lens.Test.Internals.ConverterHolder ()
holder.Convert = ((x:int) -> x * 2)
Lens.Test.Internals.DelegateShape::IsDirectConverter holder.Convert",
                true
            );
        }

        #endregion

        #region What the shape still refuses

        [Test]
        public void OverloadsDifferingOnlyInTheResultArePickedByTheBody()
        {
            // Sum takes a selector to int, to long, to double and to decimal; only the body says
            // which of them is meant, and a literal written with its argument types knows
            Test("(new [[1; 2]]).Sum ((x:int) -> x * 1.0)", 3.0);
            Test("(new [[1; 2]]).Sum ((x:int) -> x)", 3);
        }

        [Test]
        public void AResultThatOnlyConvertsIsNotAccepted()
        {
            // a lambda is compiled into a method of exactly one signature: there is no boxing step
            // to insert between a body returning int and a Func<object>
            TestError(@"
fun test:string (x:Func<object>) ->
    (x ()).ToString ()

test (-> 1)",
                CompilerMessages.FunctionNotFound
            );
        }

        [Test]
        public void AnArgumentThatDoesNotFitIsStillRefused()
        {
            TestError(@"
fun test:string (x:Func<int, int>) ->
    (x 1).ToString ()

test ((a:string) -> 1)",
                CompilerMessages.FunctionNotFound
            );
        }

        #endregion
    }
}
