using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// Members a script reaches through an interface rather than through the type it names: one
    /// interface inheriting another's members, a class inheriting a default implementation, and the
    /// static abstract members - factories and operators - that generic math is built out of.
    ///
    /// Reflection reports none of these on the type that was named, so all of them come from the
    /// interface itself, with its type arguments filled in. Getting that last part wrong is what
    /// used to make IList&lt;int&gt;.Add take a 'T' and IDictionary.Clear a call to an open generic
    /// definition.
    /// </summary>
    [TestFixture]
    internal class InterfaceMembersTest : TestBase
    {
        #region Members inherited between interfaces

        [Test]
        public void InheritedMethodTakesTheInstantiatedArgument()
        {
            // Add is declared on ICollection<T>, not on IList<T>
            Test(@"
var l = new List<int> ()
let i = l as IList<int>
i.Add 5
l.Count", 1);
        }

        [Test]
        public void InheritedMethodWithNoArguments()
        {
            // Clear likewise, and calling it used to emit a call to the open ICollection<>
            Test(@"
var d = new Dictionary<string, int> ()
d.Add ""a"" 1
let i = d as IDictionary<string, int>
i.Clear ()
d.Count", 0);
        }

        [Test]
        public void TheMoreDerivedInterfaceWins()
        {
            // both IEnumerable<int> and IEnumerable offer a GetEnumerator, and the generic one is
            // the answer rather than an ambiguity
            Test(@"
var l = new List<int> ()
l.Add 5
let i = l as IList<int>
(i.GetEnumerator ()).MoveNext ()", true);
        }

        #endregion

        // The rest needs a runtime that has default interface implementations and can dispatch
        // static abstract interface members, and .NET Framework has neither.
#if !NET_CLASSIC

        #region Default interface implementations

        [Test]
        public void DefaultMethodOnImplementingClass()
        {
            Test(@"
use Lens.Test.Internals
let b = new Bob ()
b.Greet ()", "hello, bob");
        }

        [Test]
        public void DefaultMethodThroughInterfaceCast()
        {
            Test(@"
use Lens.Test.Internals
let b = new Bob ()
(b as IGreeter).Greet ()", "hello, bob");
        }

        [Test]
        public void DefaultMethodOnConstrainedParameter()
        {
            Test(@"
use Lens.Test.Internals
fun greet<T = IGreeter>:string (x:T) -> x.Greet ()
greet (new Bob ())", "hello, bob");
        }

        [Test]
        public void ClassMemberWinsOverDefaultMethod()
        {
            Test(@"
use Lens.Test.Internals
let r = new Rob ()
r.Greet ()", "hi, rob");
        }

        [Test]
        public void DefaultMethodThroughBaseClass()
        {
            Test(@"
use Lens.Test.Internals
let b = new Bobby ()
b.Greet ()", "hello, bob");
        }

        [Test]
        public void AmbiguousDefaultMethodIsReported()
        {
            TestError(@"
use Lens.Test.Internals
let l = new Loud ()
l.Greet ()", CompilerMessages.TypeMethodInvocationAmbiguous);
        }

        #endregion

        #region Static abstract members

        [Test]
        public void StaticAbstractMethodOnConstrainedParameter()
        {
            Test(@"
use Lens.Test.Internals
fun mk<T = IZeroed<T>>:T (x:int) -> T::Make x
(mk<Num> 7).ToString ()", "7");
        }

        [Test]
        public void StaticAbstractMethodOnConcreteType()
        {
            Test(@"
use Lens.Test.Internals
(Num::Make 3).ToString ()", "3");
        }

        [Test]
        public void DeclaredOperatorOnConstrainedParameter()
        {
            Test(@"
use Lens.Test.Internals
fun sum<T = IAddable<T>>:T (a:T b:T) -> a + b
(sum (new Money ()) (new Money ())).ToString ()", "0");
        }

        #endregion

        #region Operators inherited from a constraint interface

        [Test]
        public void InheritedOperatorOnConstrainedParameter()
        {
            // INumber<T> inherits op_Addition from IAdditionOperators<TSelf, TOther, TResult>
            Test(@"
use System.Numerics
fun add2<T = INumber<T>>:T (a:T b:T) -> a + b
add2 20 22", 42);
        }

        [Test]
        public void InheritedComparisonOnConstrainedParameter()
        {
            Test(@"
use System.Numerics
fun mx<T = INumber<T>>:bool (a:T b:T) -> a > b
mx 5 3", true);
        }

        [Test]
        public void ArithmeticOnPlainNumbersIsStillNative()
        {
            // Int32 implements IAdditionOperators, and picking its abstract declaration over the
            // 'add' opcode is invalid IL rather than a slower way to add
            Test(@"
var acc = 0
for i in 1..5 do
    acc = acc + i
acc", 10);
        }

        #endregion

#endif
    }
}
