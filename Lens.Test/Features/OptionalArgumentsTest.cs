using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    /// <summary>
    /// Calls that leave the trailing arguments out, and let the callee's declaration say what is
    /// passed for them.
    /// </summary>
    [TestFixture]
    internal class OptionalArgumentsTest : TestBase
    {
        #region Methods

        [Test]
        public void AllDefaultsAreFilledIn()
        {
            Test(@"
use Lens.Test.Internals
Optionals::Opt 1", "1/5/z");
        }

        [Test]
        public void SomeDefaultsAreFilledIn()
        {
            Test(@"
use Lens.Test.Internals
Optionals::Opt 1 2", "1/2/z");
        }

        [Test]
        public void EveryArgumentSpelledOut()
        {
            Test(@"
use Lens.Test.Internals
Optionals::Opt 1 2 ""q""", "1/2/q");
        }

        [Test]
        public void MethodWithNoDefaultToFillWins()
        {
            Test(@"
use Lens.Test.Internals
Optionals::Pick 1", "one:1");
        }

        [Test]
        public void DefaultsOfEveryKind()
        {
            Test(@"
use Lens.Test.Internals
Optionals::Kinds ()", "0/3/x/High/1.5/7/null/True");
        }

        [Test]
        public void DefaultsAfterAGivenArgument()
        {
            Test(@"
use Lens.Test.Internals
Optionals::Kinds 9", "9/3/x/High/1.5/7/null/True");
        }

        [Test]
        public void DefaultOnInstanceMethod()
        {
            Test(@"
use Lens.Test.Internals
let o = new Optionals ()
o.Instance ""a""", "a/True");
        }

        [Test]
        public void DefaultOnGenericMethod()
        {
            Test(@"
use Lens.Test.Internals
Optionals::Generic 42", "42/t");
        }

        /// <summary>
        /// A partial application spells the arguments it is given and names the ones it is not;
        /// the defaults are filled into the call the lambda it becomes makes.
        /// </summary>
        [Test]
        public void DefaultIsFilledInAPartialApplication()
        {
            Test(@"
use Lens.Test.Internals
let fx = Optionals::Opt _
fx 3", "3/5/z");
        }

        [Test]
        public void DefaultIsFilledInsideALambda()
        {
            Test(@"
use Lens.Test.Internals
let fx = (a:int) -> Optionals::Opt a
fx 3", "3/5/z");
        }

        /// <summary>
        /// An '[Optional]' parameter carrying no value says nothing about what to pass, and a call
        /// that leaves it out must be reported rather than guessed at.
        /// </summary>
        [Test]
        public void OptionalWithoutValueIsNotFilledIn()
        {
            TestError(@"
use Lens.Test.Internals
Optionals::NoValue ()", CompilerMessages.TypeMethodArgumentsMismatch);
        }

        [Test]
        public void TooFewArgumentsIsStillAnError()
        {
            TestError(@"
use Lens.Test.Internals
Optionals::Opt ()", CompilerMessages.TypeMethodArgumentsMismatch);
        }

        #endregion

        #region Constructors

        [Test]
        public void ConstructorDefaultIsFilledIn()
        {
            Test(@"
use Lens.Test.Internals
(new OptionalCtor 1).Value", "1/def");
        }

        [Test]
        public void ConstructorArgumentSpelledOut()
        {
            Test(@"
use Lens.Test.Internals
(new OptionalCtor 1 ""x"").Value", "1/x");
        }

        // System.Index arrived with .NET Core 3.0 and is not in the .NET Framework box
#if !NET_CLASSIC

        /// <summary>
        /// The reason this matters against the modern BCL at all: System.Index takes a flag that
        /// every call site but one leaves out.
        /// </summary>
        [Test]
        public void IndexIsBuiltFromOneArgument()
        {
            Test(@"
let a = new [1; 2; 3]
let i = new System.Index 1
a[i]", 2);
        }

#endif

        #endregion

        #region Indexers

        [Test]
        public void IndexerDefaultIsFilledIn()
        {
            Test(@"
use Lens.Test.Internals
let o = new OptionalIndexer ()
o[1]", "1/d/-");
        }

        [Test]
        public void IndexerIndexSpelledOut()
        {
            Test(@"
use Lens.Test.Internals
let o = new OptionalIndexer ()
o[1; ""q""]", "1/q/-");
        }

        [Test]
        public void IndexerSetterDefaultIsFilledIn()
        {
            Test(@"
use Lens.Test.Internals
let o = new OptionalIndexer ()
o[1] = ""v""
o.Stored", "1/d/v");
        }

        #endregion

        #region Extension methods

        [Test]
        public void ExtensionMethodDefaultIsFilledIn()
        {
            Test(@"
use Lens.Test.Internals
""hi"".Tagged ()", "hi!");
        }

        [Test]
        public void ExtensionMethodArgumentSpelledOut()
        {
            Test(@"
use Lens.Test.Internals
""hi"".Tagged ""?""", "hi?");
        }

        #endregion
    }
}
