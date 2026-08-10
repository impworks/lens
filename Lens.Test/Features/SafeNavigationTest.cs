using System;
using System.Collections.Generic;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class SafeNavigationTest : TestBase
    {
        #region Short-circuiting

        [Test]
        public void ChainIsNotEvaluatedAtAll()
        {
            Chainable.Calls = 0;

            TestConfigured(
                Setup(null),
                "root?.Next.Next.Value",
                null
            );

            Assert.AreEqual(0, Chainable.Calls, "The chain must not be evaluated when the root is null!");
        }

        [Test]
        public void ChainIsEvaluatedWhenNotNull()
        {
            Chainable.Calls = 0;

            TestConfigured(
                Setup(new Chainable()),
                "root?.Next.Next.Value",
                42
            );

            Assert.AreEqual(2, Chainable.Calls);
        }

        [Test]
        public void ManyChecksStopAtTheFirstNull()
        {
            Chainable.Calls = 0;

            TestConfigured(
                Setup(null),
                "root?.Next?.Next?.Value",
                null
            );

            Assert.AreEqual(0, Chainable.Calls);
        }

        [Test]
        public void ManyChecksInTheMiddleOfAChain()
        {
            Chainable.Calls = 0;

            TestConfigured(
                Setup(new Chainable {IsLast = true}),
                "root?.Next?.Next?.Value",
                null
            );

            // the first link is evaluated and returns null, the second one is never reached
            Assert.AreEqual(1, Chainable.Calls);
        }

        [Test]
        public void ReceiverIsEvaluatedOnlyOnce()
        {
            Chainable.Calls = 0;

            TestConfigured(
                Setup(new Chainable()),
                "root?.Next?.Value",
                42
            );

            Assert.AreEqual(1, Chainable.Calls);
        }

        #endregion

        #region Result typing

        [Test]
        public void ReferenceTypeResult()
        {
            TestConfigured(StringSetup("abc"), @"str?.ToUpper ()", "ABC");
            TestConfigured(StringSetup(null), @"str?.ToUpper ()", null);
        }

        [Test]
        public void ValueTypeResultIsLifted()
        {
            TestConfigured(StringSetup("abc"), "str?.Length", 3);
            TestConfigured(StringSetup(null), "str?.Length", null);
        }

        [Test]
        public void NullableResultIsNotLiftedTwice()
        {
            TestConfigured(
                ctx =>
                {
                    ctx.RegisterType(typeof(Chainable));
                    ctx.RegisterProperty("root", () => new Chainable());
                },
                "root?.MaybeValue",
                1337
            );
        }

        [Test]
        public void UnitResult()
        {
            var list = new List<int>();

            TestConfigured(
                ctx => ctx.RegisterProperty("items", () => list),
                "items?.Add 1",
                null
            );

            Assert.AreEqual(new[] {1}, list.ToArray());
        }

        [Test]
        public void UnitResultIsSkippedWhenNull()
        {
            TestConfigured(
                ctx => ctx.RegisterProperty("items", () => (List<int>) null),
                "items?.Add 1",
                null
            );
        }

        #endregion

        #region Receiver typing

        [Test]
        public void NullableReceiver()
        {
            TestConfigured(NullableSetup(5), @"num?.ToString ()", "5");
            TestConfigured(NullableSetup(null), @"num?.ToString ()", null);
        }

        [Test]
        public void NullableReceiverInTheMiddleOfAChain()
        {
            TestConfigured(NullableSetup(255), @"num?.ToString ""X2""", "FF");
            TestConfigured(NullableSetup(null), @"num?.ToString ""X2""", null);
        }

        [Test]
        public void ValueTypeReceiverIsAnError()
        {
            TestError(@"
let a = 1
a?.ToString ()",
                CompilerMessages.NullSafeOperatorValueType
            );
        }

        #endregion

        #region Invocation

        [Test]
        public void NullSafeInvocationWithArguments()
        {
            TestConfigured(StringSetup("hello"), @"str?.Substring 1 3", "ell");
            TestConfigured(StringSetup(null), @"str?.Substring 1 3", null);
        }

        [Test]
        public void NullSafeInvocationDoesNotEvaluateArguments()
        {
            Chainable.Calls = 0;

            TestConfigured(
                ctx =>
                {
                    ctx.RegisterType(typeof(Chainable));
                    ctx.RegisterProperty("root", () => (Chainable) null);
                    ctx.RegisterProperty("bump", () => new Chainable().Next);
                },
                "root?.Echo bump",
                null
            );

            Assert.AreEqual(0, Chainable.Calls);
        }

        #endregion

        #region Indexers

        [Test]
        public void IndexerOnArray()
        {
            TestConfigured(
                ctx => ctx.RegisterProperty("arr", () => new[] {1, 2, 3}),
                "arr?[1]",
                2
            );
        }

        [Test]
        public void IndexerOnNullArray()
        {
            TestConfigured(
                ctx => ctx.RegisterProperty("arr", () => (int[]) null),
                "arr?[1]",
                null
            );
        }

        [Test]
        public void IndexerOnList()
        {
            TestConfigured(
                ctx => ctx.RegisterProperty("items", () => new List<string> {"a", "b"}),
                @"items?[1]",
                "b"
            );

            TestConfigured(
                ctx => ctx.RegisterProperty("items", () => (List<string>) null),
                @"items?[1]",
                null
            );
        }

        [Test]
        public void IndexerOnDictionary()
        {
            TestConfigured(
                ctx => ctx.RegisterProperty("lookup", () => new Dictionary<string, int> {{"a", 1}}),
                @"lookup?[""a""]",
                1
            );

            TestConfigured(
                ctx => ctx.RegisterProperty("lookup", () => (Dictionary<string, int>) null),
                @"lookup?[""a""]",
                null
            );
        }

        [Test]
        public void NullableArrayTypeStillParses()
        {
            // "?[" must not swallow the array brackets of a nullable array type
            Test(@"
var xs : int?[]
xs == null",
                true
            );
        }

        #endregion

        #region Interaction with other operators

        [Test]
        public void CoalesceWithNullSafeChain()
        {
            TestConfigured(StringSetup("abcd"), "str?.Length ?? -1", 4);
            TestConfigured(StringSetup(null), "str?.Length ?? -1", -1);
        }

        [Test]
        public void CoalesceWithReferenceResult()
        {
            TestConfigured(StringSetup(null), @"str?.ToUpper () ?? ""none""", "none");
        }

        [Test]
        public void NullSafeChainInACondition()
        {
            TestConfigured(StringSetup(null), "if str?.Length == null then 1 else 2", 1);
        }

        [Test]
        public void NullSafeChainInsideAnInterpolatedString()
        {
            TestConfigured(StringSetup(null), @"$""[{str?.Length}]""", "[]");
            TestConfigured(StringSetup("ab"), @"$""[{str?.Length}]""", "[2]");
        }

        [Test]
        public void NullSafeChainAsAReceiver()
        {
            TestConfigured(StringSetup("abc"), @"(str?.Length).ToString ()", "3");
        }

        [Test]
        public void NullSafeChainInsideALambda()
        {
            Test(@"
let f = (s:string) -> s?.Length
f null",
                null
            );

            Test(@"
let f = (s:string) -> s?.Length
f ""ab""",
                2
            );
        }

        [Test]
        public void NullSafeChainOnAStaticMember()
        {
            Test(@"string::Empty?.Length", 0);
        }

        [Test]
        public void NullSafeChainInALoop()
        {
            TestConfigured(
                ctx => ctx.RegisterProperty("items", () => new[] {"a", null, "ccc"}),
                @"
var total = 0
for x in items do
    total = total + (x?.Length ?? 0)
total",
                4
            );
        }

        #endregion

        #region Rejections

        [Test]
        public void NullSafeAssignmentIsAnError()
        {
            TestError("a?.b = 1", ParserMessages.NullSafeAccessorNotAllowed);
        }

        [Test]
        public void NullSafeIndexerAssignmentIsAnError()
        {
            TestError("a?[0] = 1", ParserMessages.NullSafeAccessorNotAllowed);
        }

        [Test]
        public void NullSafeAssignmentDeepInAChainIsAnError()
        {
            TestError("a?.b.c = 1", ParserMessages.NullSafeAccessorNotAllowed);
        }

        [Test]
        public void NullSafeShorthandAssignmentIsAnError()
        {
            TestError("a?.b += 1", ParserMessages.NullSafeAccessorNotAllowed);
        }

        [Test]
        public void NullableTypeFollowedByAMemberAccessIsNotSupported()
        {
            // "int?.ToString ()" lexes as a null-safe access on the identifier "int" rather than
            // as a member of the "int?" type: writing it out as "(1 as int?).ToString ()" is required
            TestError(@"int?.ToString ()", CompilerMessages.IdentifierNotFound);
        }

        [Test]
        public void NullSafeRefArgumentIsAnError()
        {
            TestError(@"int::TryParse ""1"" (ref a?.b)", ParserMessages.NullSafeAccessorNotAllowed);
        }

        #endregion

        #region Helpers

        private static Action<LensCompiler> Setup(Chainable root)
        {
            return ctx =>
            {
                ctx.RegisterType(typeof(Chainable));
                ctx.RegisterProperty("root", () => root);
            };
        }

        private static Action<LensCompiler> StringSetup(string value)
        {
            return ctx => ctx.RegisterProperty("str", () => value);
        }

        private static Action<LensCompiler> NullableSetup(int? value)
        {
            return ctx => ctx.RegisterProperty("num", () => value);
        }

        #endregion
    }

    /// <summary>
    /// Helper class for testing null-safe accessor chains.
    /// </summary>
    public class Chainable
    {
        /// <summary>
        /// Number of times the chain has been walked.
        /// </summary>
        public static int Calls;

        /// <summary>
        /// Flag indicating that Next returns null.
        /// </summary>
        public bool IsLast;

        public Chainable Next
        {
            get
            {
                Calls++;
                return IsLast ? null : this;
            }
        }

        public int Value => 42;

        public int? MaybeValue => 1337;

        public string Echo(Chainable other)
        {
            return "echo";
        }
    }

}
