using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lens.Compiler;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class SafeModeTest : TestBase
    {
        [Test]
        public void BlacklistNamespaces1()
        {
            var opts = new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitNamespaces = new List<string> {"System.Text"}
            };

            var src = @"new System.Text.RegularExpressions.Regex ""test""";
            try
            {
                Compile(src, opts);
                Assert.Fail();
            }
            catch (LensCompilerException ex)
            {
                Assert.AreEqual(
                    string.Format(CompilerMessages.SafeModeIllegalType, typeof(Regex).FullName),
                    ex.Message
                );
            }
        }

        [Test]
        public void BlacklistNamespaces2()
        {
            var opts = new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitNamespaces = new List<string> {"System.Text"}
            };

            var src = @"
use System.Text.RegularExpressions
new List<Regex> ()
";
            try
            {
                Compile(src, opts);
                Assert.Fail();
            }
            catch (LensCompilerException ex)
            {
                Assert.AreEqual(
                    string.Format(CompilerMessages.SafeModeIllegalType, typeof(List<Regex>).FullName),
                    ex.Message
                );
            }
        }

        [Test]
        public void BlacklistTypes1()
        {
            var opts = new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitTypes = new List<string> {"System.Collections.Stack"}
            };

            var src = @"
use System.Collections
var s = new Stack ()
s.Push 1
";
            try
            {
                Compile(src, opts);
                Assert.Fail();
            }
            catch (LensCompilerException ex)
            {
                Assert.AreEqual(
                    string.Format(CompilerMessages.SafeModeIllegalType, typeof(System.Collections.Stack).FullName),
                    ex.Message
                );
            }
        }

        [Test]
        public void BlacklistTypes2()
        {
            var opts = new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitTypes = new List<string> {"System.GC"}
            };

            var src = @"
GC::Collect ()
";
            try
            {
                Compile(src, opts);
                Assert.Fail();
            }
            catch (LensCompilerException ex)
            {
                Assert.AreEqual(
                    string.Format(CompilerMessages.SafeModeIllegalType, typeof(GC).FullName),
                    ex.Message
                );
            }
        }

        [Test]
        public void BlacklistEnvironment1()
        {
            var src = @"GC::Collect ()";
            TestSubsystem(typeof(GC), SafeModeSubsystem.Environment, src);
        }

        [Test]
        public void BlacklistEnvironment2()
        {
            var src = @"System.Environment::StackTrace";
            TestSubsystem(typeof(Environment), SafeModeSubsystem.Environment, src);
        }

        [Test]
        public void BlacklistEnvironment3()
        {
            var src = @"System.AppDomain::CurrentDomain.IsFullyTrusted";
            TestSubsystem(typeof(AppDomain), SafeModeSubsystem.Environment, src);
        }

        [Test]
        public void BlacklistEnvironment4()
        {
            var src = @"System.Diagnostics.Debug::WriteLine ""test""";
            TestSubsystem(typeof(System.Diagnostics.Debug), SafeModeSubsystem.Environment, src);
        }

        [Test]
        public void BlacklistEnvironment5()
        {
            var src = @"System.Runtime.InteropServices.Marshal::IsComObject (new object ())";
            TestSubsystem(typeof(System.Runtime.InteropServices.Marshal), SafeModeSubsystem.Environment, src);
        }

        [Test]
        public void BlacklistIO1()
        {
            var src = @"System.IO.Directory::Exists ""C:\\A\\B""";
            TestSubsystem(typeof(System.IO.Directory), SafeModeSubsystem.IO, src);
        }

        [Test]
        public void BlacklistIO2()
        {
            var src = @"System.IO.IsolatedStorage.IsolatedStorageFile::IsEnabled";
            TestSubsystem(typeof(System.IO.IsolatedStorage.IsolatedStorageFile), SafeModeSubsystem.IO, src);
        }

        [Test]
        public void BlacklistThreading1()
        {
            var src = @"
var workThreads = 0
var cpThreads = 0
System.Threading.ThreadPool::GetAvailableThreads (ref workThreads) (ref cpThreads)";
            TestSubsystem(typeof(System.Threading.ThreadPool), SafeModeSubsystem.Threading, src);
        }

        [Test]
        public void BlacklistThreading2()
        {
            var src = @"System.Threading.Tasks.Task::Run (-> print ""hello world!"")";
            TestSubsystem(typeof(System.Threading.Tasks.Task), SafeModeSubsystem.Threading, src);
        }

        [Test]
        public void BlacklistReflection1()
        {
            var src = @"System.Reflection.Assembly::GetCallingAssembly()";
            TestSubsystem(typeof(System.Reflection.Assembly), SafeModeSubsystem.Reflection, src);
        }

        [Test]
        public void BlacklistReflection2()
        {
            var src = @"System.AppDomain::CurrentDomain.IsFullyTrusted";
            TestSubsystem(typeof(AppDomain), SafeModeSubsystem.Reflection, src);
        }

        [Test]
        public void BlacklistReflection3()
        {
            var src = @"(typeof int).Fullname";
            TestSubsystem(typeof(Type), SafeModeSubsystem.Reflection, src);
        }

        /// <summary>
        /// AssemblyLoadContext is the modern .NET replacement for the AppDomain-based loading tricks
        /// the Reflection subsystem exists to block, so it must be covered too.
        /// </summary>
        [Test]
        public void BlacklistReflection4()
        {
            // referenced by name because the type does not exist on .NET Framework
            var type = Type.GetType("System.Runtime.Loader.AssemblyLoadContext, System.Runtime.Loader");
            if (type == null)
                Assert.Ignore("AssemblyLoadContext is a modern .NET API.");

            var src = @"System.Runtime.Loader.AssemblyLoadContext::Default";
            TestSubsystem(type, SafeModeSubsystem.Reflection, src);
        }

        [Test]
        public void BlacklistNetwork1()
        {
            var src = @"new System.Net.HttpListener ()";
            TestSubsystem(typeof(System.Net.HttpListener), SafeModeSubsystem.Network, src);
        }

        [Test]
        public void BlacklistNetwork2()
        {
            var src = @"System.Net.Sockets.Socket::OSSupportsIPv4";
            TestSubsystem(typeof(System.Net.Sockets.Socket), SafeModeSubsystem.Network, src);
        }

        #region Generics

        /// <summary>
        /// A generic parameter has no full name, and safe mode used to hand that null straight to a
        /// dictionary: every generic function failed with "Value cannot be null. (Parameter 'key')"
        /// as soon as any safe mode was on.
        /// </summary>
        [Test]
        public void GenericFunctionUnderBlacklist()
        {
            var src = @"
fun id<T>:T (x:T) -> x
id 42";

            Test(src, 42, Blacklisting("System.IO"));
        }

        [Test]
        public void GenericRecordUnderBlacklist()
        {
            var src = @"
record Box<T>
    Value: T

let b = new Box<int> 42
b.Value";

            Test(src, 42, Blacklisting("System.IO"));
        }

        /// <summary>
        /// An array of a generic parameter has no full name of its own either.
        /// </summary>
        [Test]
        public void GenericArrayArgumentUnderBlacklist()
        {
            var src = @"
fun firstOf<T>:T (items:T[]) -> items[0]
firstOf (new [3; 4; 5])";

            Test(src, 3, Blacklisting("System.IO"));
        }

        /// <summary>
        /// A whitelist is the stricter reading, and the one that would refuse a placeholder if it
        /// were weighed as though it were a type of its own.
        /// </summary>
        [Test]
        public void GenericFunctionUnderWhitelist()
        {
            var opts = new LensCompilerOptions
            {
                SafeMode = SafeMode.Whitelist,
                SafeModeExplicitNamespaces = new List<string> {"System"}
            };

            var src = @"
fun id<T>:T (x:T) -> x
id 42";

            Test(src, 42, opts);
        }

        /// <summary>
        /// The parameter being waved through must not wave through what it is substituted with.
        /// </summary>
        [Test]
        public void GenericArgumentIsStillChecked()
        {
            var opts = new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitNamespaces = new List<string> {"System.Text"}
            };

            var src = @"
use System.Text.RegularExpressions
new List<Regex> ()
";

            try
            {
                Compile(src, opts);
                Assert.Fail();
            }
            catch (LensCompilerException ex)
            {
                Assert.AreEqual(
                    string.Format(CompilerMessages.SafeModeIllegalType, typeof(List<Regex>).FullName),
                    ex.Message
                );
            }
        }

        private static LensCompilerOptions Blacklisting(string nsp)
        {
            return new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitNamespaces = new List<string> {nsp}
            };
        }

        #endregion

        #region Escapes

        // Every test in this region is a way a script used to get out of safe mode entirely, found
        // by asking of each rule "and what reaches the same capability without matching it?". They
        // are worth keeping as tests rather than as a note, because each one is a hole that a
        // plausible future change to the rules would reopen.

        /// <summary>
        /// The shortest escape there ever was: the compiler is a type like any other, so a script
        /// built a second one with the default options - which is to say with no safe mode at all -
        /// and ran whatever it liked through it.
        /// </summary>
        [Test]
        public void AScriptMayNotReachTheCompilerRunningIt()
        {
            var src = @"
var opts = new Lens.LensCompilerOptions ()
var lens = new Lens.LensCompiler opts
lens.Run ""System.Environment::MachineName""";

            TestSafeModeError(src, BlacklistingTypes("System.Environment"), "Lens.LensCompilerOptions");
        }

        /// <summary>
        /// Type.GetType is handed the name of a type as a string, so no rule about which types may
        /// be named ever sees what comes back out of it. This read the host's machine name through
        /// a blacklist that named System.Environment explicitly.
        /// </summary>
        [Test]
        public void AScriptMayNotLookUpATypeByName()
        {
            var src = @"
let t = System.Type::GetType ""System.Environment""
let p = t.GetProperty ""MachineName""
p.GetValue (null as object)";

            TestSafeModeMemberError(src, BlacklistingTypes("System.Environment"), "GetType", typeof(Type));
        }

        /// <summary>
        /// The same door from the other side: a Type obtained anywhere at all used to be enough,
        /// because Activator turns one into an instance.
        /// </summary>
        [Test]
        public void AScriptMayNotReachActivator()
        {
            TestSafeModeError(
                @"System.Activator::CreateInstance (typeof int)",
                BlacklistingTypes("System.IO.File"),
                "System.Activator"
            );
        }

        /// <summary>
        /// Reflection is denied in every safe mode, including under a whitelist that named the
        /// namespace it lives under. It is not a capability the host is choosing between - it is
        /// the way around whatever else the host chose.
        /// </summary>
        [Test]
        public void ReflectionIsDeniedWhateverTheRulesSay()
        {
            var opts = WhitelistingSystem();

            TestSafeModeMemberError(@"System.Type::GetType ""System.Environment""", opts, "GetType", typeof(Type));
            TestSafeModeError(@"new System.Reflection.AssemblyName ""x""", opts, typeof(System.Reflection.AssemblyName).FullName);
            TestSafeModeError(@"System.Runtime.InteropServices.Marshal::IsComObject (new object ())", opts, typeof(System.Runtime.InteropServices.Marshal).FullName);
        }

        /// <summary>
        /// typeof resolves to System.Type whatever its operand was, so the check every node goes
        /// through only ever saw System.Type and never the name the script actually wrote.
        /// </summary>
        [Test]
        public void TypeofSeesItsOperand()
        {
            TestSafeModeError(@"typeof System.IO.File", BlacklistingTypes("System.IO.File"), "System.IO.File");
        }

        /// <summary>
        /// The full name of File[] is 'System.IO.File[]', which is not the name a rule about File
        /// is written with. An array is as allowed as its element type, and no more.
        /// </summary>
        [Test]
        public void AnArrayIsAsAllowedAsItsElement()
        {
            TestSafeModeError(@"new System.IO.FileInfo[2]", BlacklistingTypes("System.IO.FileInfo"), "System.IO.FileInfo[]");
        }

        /// <summary>
        /// A constructed generic carries its arguments in its own full name, so a rule naming the
        /// type never matched the instantiation the script wrote. Both the CLR spelling with the
        /// arity suffix and the plain one have to work.
        /// </summary>
        [Test]
        public void AGenericTypeCanBeNamedByARule()
        {
            var expected = typeof(List<int>).FullName;

            TestSafeModeError(@"new System.Collections.Generic.List<int> ()", BlacklistingTypes("System.Collections.Generic.List`1"), expected);
            TestSafeModeError(@"new System.Collections.Generic.List<int> ()", BlacklistingTypes("System.Collections.Generic.List"), expected);
        }

        /// <summary>
        /// A catch clause names a type, and the node's own type is unit, so the ordinary check
        /// never looked at it.
        /// </summary>
        [Test]
        public void ACatchClauseNamesAType()
        {
            var src = @"
try
    1
catch ex:System.IO.FileNotFoundException
    2";

            TestSafeModeError(src, BlacklistingTypes("System.IO.FileNotFoundException"), "System.IO.FileNotFoundException");
        }

        /// <summary>
        /// The type an extension method is declared on is never named in the script - that is the
        /// point of an extension method - so the call site is the only place a rule about it can be
        /// applied.
        /// </summary>
        [Test]
        public void AnExtensionMethodBringsItsDeclaringTypeWithIt()
        {
            TestSafeModeError(
                @"(new [1; 2; 3]).Where (x:int -> x > 1)",
                BlacklistingTypes("System.Linq.Enumerable"),
                "System.Linq.Enumerable"
            );
        }

        /// <summary>
        /// A signature is a mention of a type like any other. This reported the problem at the
        /// first call for a return type, and not at all for an argument type.
        /// </summary>
        [Test]
        public void ASignatureNamesTypes()
        {
            var src = @"
fun f:int (x:System.IO.FileInfo) -> 1
1";

            TestSafeModeError(src, BlacklistingTypes("System.IO.FileInfo"), "System.IO.FileInfo");

            var returning = @"
fun f:System.IO.FileInfo -> null
1";

            TestSafeModeError(returning, BlacklistingTypes("System.IO.FileInfo"), "System.IO.FileInfo");
        }

        #endregion

        #region Rule matching

        /// <summary>
        /// A namespace rule covers a namespace and the ones nested in it, and stops at a dot. The
        /// check used to be a StartsWith, which answered yes to 'System.Text' for a rule about
        /// 'System.Te' - and, the way that matters, would have answered yes under a whitelist too.
        /// </summary>
        [Test]
        public void ANamespaceRuleStopsAtADot()
        {
            Assert.DoesNotThrow(() => Compile(@"new System.Text.RegularExpressions.Regex ""x""", Blacklisting("System.Te")));

            TestSafeModeError(
                @"new System.Text.RegularExpressions.Regex ""x""",
                Blacklisting("System.Text"),
                typeof(Regex).FullName
            );
        }

        /// <summary>
        /// A rule naming a namespace covers the ones nested inside it. Read as a whitelist that is
        /// what makes 'System' close to no restriction at all, since the whole of the BCL sits
        /// under it - which is worth knowing before writing one.
        /// </summary>
        [Test]
        public void ANamespaceRuleCoversNestedNamespaces()
        {
            Test(@"(new System.Text.RegularExpressions.Regex ""a+"").IsMatch ""aaa""", true, WhitelistingSystem());
        }

        /// <summary>
        /// A member rule denies one member of an otherwise allowed type, which is the only
        /// granularity that can express "this type is fine, that one call on it is not".
        /// </summary>
        [Test]
        public void AMemberRuleDeniesOneMember()
        {
            var opts = new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitMembers = new List<string> {"System.String::ToUpper"}
            };

            TestSafeModeMemberError(@"""hello"".ToUpper ()", opts, "ToUpper", typeof(string));

            // the rest of the type is untouched
            Test(@"""HELLO"".ToLower ()", "hello", opts);
        }

        /// <summary>
        /// A member rule is a deny rule in both modes: a whitelist of members would mean naming
        /// every method a script is allowed to call, which is not a list anyone maintains correctly.
        /// </summary>
        [Test]
        public void AMemberRuleDeniesUnderAWhitelistToo()
        {
            var opts = WhitelistingSystem();
            opts.SafeModeExplicitMembers = new List<string> {"System.String::ToUpper"};

            TestSafeModeMemberError(@"""hello"".ToUpper ()", opts, "ToUpper", typeof(string));
            Test(@"""HELLO"".ToLower ()", "hello", opts);
        }

        /// <summary>
        /// A rule that silently fails to match is the worst outcome available here, so a member
        /// rule that is not one is rejected rather than ignored.
        /// </summary>
        [Test]
        public void AMalformedMemberRuleIsRejected()
        {
            var opts = new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitMembers = new List<string> {"System.String.ToUpper"}
            };

            Assert.Throws<ArgumentException>(() => Compile("1", opts));
        }

        /// <summary>
        /// Whitespace and blank entries are what a hand-written list picks up, and neither should
        /// quietly turn a rule off.
        /// </summary>
        [Test]
        public void RuleWhitespaceIsIgnored()
        {
            var opts = new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitTypes = new List<string> {"  System.IO.FileInfo  ", "", "   "}
            };

            TestSafeModeError(@"new System.IO.FileInfo ""x""", opts, "System.IO.FileInfo");
        }

        #endregion

        #region Whitelist usability

        // A whitelist is the only defensible posture for a script that came from outside, and it
        // used to be unusable: the script's own records and the compiler's own unit type have no
        // namespace a host could whitelist, so the first line of almost any script was refused.

        [Test]
        public void AWhitelistAllowsTheScriptsOwnRecords()
        {
            var src = @"
record Box
    Value:int

let b = new Box 1
b.Value";

            Test(src, 1, WhitelistingSystem());
        }

        [Test]
        public void AWhitelistAllowsLambdas()
        {
            var src = @"
let f = (x:int) -> x + 1
f 1";

            Test(src, 2, WhitelistingSystem());
        }

        [Test]
        public void AWhitelistAllowsStatementsWithNoValue()
        {
            var src = @"
print ""hi""
let x = 1
x";

            Test(src, 1, WhitelistingSystem());
        }

        #endregion

        #region Subsystems

        /// <summary>
        /// The Environment subsystem used to deny the namespace 'System.Runtime' by prefix, which
        /// took System.Runtime.CompilerServices with it - so turning the subsystem on made every
        /// await in every script fail to compile. The parts of System.Runtime that are actually
        /// dangerous are denied by the core rules instead, in every mode.
        /// </summary>
        [Test]
        public void TheEnvironmentSubsystemDoesNotBreakAwait()
        {
            var src = @"
use System.Threading.Tasks

fun fetch:Task<int> ->
    await (Task::FromResult 21)

1";

            Test(src, 1, new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitSubsystems = SafeModeSubsystem.Environment
            });
        }

        /// <summary>
        /// A subsystem the host did not name stays available, which is what makes the flags worth
        /// having over a hand-written list.
        /// </summary>
        [Test]
        public void ASubsystemThatWasNotNamedIsAllowed()
        {
            Test(@"System.IO.Path::GetExtension ""a.txt""", ".txt", new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitSubsystems = SafeModeSubsystem.Network
            });
        }

        #endregion

        #region Helpers

        private static LensCompilerOptions BlacklistingTypes(params string[] types)
        {
            return new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitTypes = new List<string>(types)
            };
        }

        private static LensCompilerOptions WhitelistingSystem()
        {
            return new LensCompilerOptions
            {
                SafeMode = SafeMode.Whitelist,
                SafeModeExplicitNamespaces = new List<string> {"System"}
            };
        }

        private void TestSafeModeError(string src, LensCompilerOptions opts, string expectedType)
        {
            var ex = Assert.Throws<LensCompilerException>(() => Compile(src, opts));

            Assert.AreEqual(
                string.Format(CompilerMessages.SafeModeIllegalType, expectedType),
                ex.Message
            );
        }

        private void TestSafeModeMemberError(string src, LensCompilerOptions opts, string member, Type declaringType)
        {
            var ex = Assert.Throws<LensCompilerException>(() => Compile(src, opts));

            Assert.AreEqual(
                string.Format(CompilerMessages.SafeModeIllegalMember, member, declaringType.FullName),
                ex.Message
            );
        }

        #endregion

        private void TestSubsystem(Type type, SafeModeSubsystem system, string code)
        {
            var opts = new LensCompilerOptions
            {
                SafeMode = SafeMode.Blacklist,
                SafeModeExplicitSubsystems = system
            };

            try
            {
                Compile(code, opts);
                Assert.Fail();
            }
            catch (LensCompilerException ex)
            {
                Assert.AreEqual(
                    string.Format(CompilerMessages.SafeModeIllegalType, type.FullName),
                    ex.Message
                );
            }
        }
    }
}