using System;
using Lens.Test.Internals;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class DeclarationsTest : TestBase
    {
        #region Positive cases

        [Test]
        public void DeclaredEnvironmentIsUsable()
        {
            TestConfigured(
                ctx =>
                {
                    ctx.RegisterProperty("half", () => 21);
                    ctx.RegisterFunction("addNumbers", typeof(ImportableStaticMethods).GetMethod("AddNumbers"));
                },
                @"
declare
    let half:int
    fun addNumbers:int (a:int b:int)

addNumbers half half",
                42
            );
        }

        [Test]
        public void DeclarationsEmitNoCode()
        {
            TestConfigured(
                ctx => { ctx.RegisterProperty("half", () => 21); },
                @"
declare
    let half:int",
                null
            );
        }

        [Test]
        public void TypeAliasIsDefinedByTheDeclaration()
        {
            // no RegisterType call: given the referenced assemblies, the alias needs no host support
            TestConfigured(
                ctx => { },
                @"
declare
    type Dt = System.DateTime

Dt::MaxValue.Year",
                9999
            );
        }

        [Test]
        public void TypeAliasMatchingTheHostRegistrationIsAccepted()
        {
            TestConfigured(
                ctx => { ctx.RegisterType("Dt", typeof(DateTime)); },
                @"
declare
    type Dt = System.DateTime

Dt::MaxValue.Year",
                9999
            );
        }

        [Test]
        public void VoidFunctionIsDeclaredWithoutAReturnType()
        {
            TestConfigured(
                ctx => { ctx.RegisterFunction("doNothing", typeof(ImportableStaticMethods).GetMethod("DoNothing")); },
                @"
declare
    fun doNothing

doNothing ()",
                null
            );
        }

        [Test]
        public void OverloadsAreDeclaredOneByOne()
        {
            TestConfigured(
                ctx => { ctx.RegisterFunctionOverloads(typeof(ImportableStaticMethods), "OverloadedAdd", "add"); },
                @"
declare
    fun add:double (a:double b:double)
    fun add:string (a:string b:string)

add ""a"" ""b""",
                "ab"
            );
        }

        [Test]
        public void VariadicFunctionIsDeclaredWithEllipsis()
        {
            TestConfigured(
                ctx => { ctx.RegisterFunction("sum", typeof(ImportableStaticMethods).GetMethod("Sum")); },
                @"
declare
    fun sum:int (numbers:int...)

sum 1 2 3",
                6
            );
        }

        [Test]
        public void DelegateRegisteredAsFunctionSatisfiesTheDeclaration()
        {
            // RegisterFunction(name, delegate) registers a readonly property, not a method:
            // both shapes are called the same way, so both satisfy a declared function
            Func<int, int> doubler = x => x * 2;
            TestConfigured(
                ctx => { ctx.RegisterFunction("doubler", doubler); },
                @"
declare
    fun doubler:int (x:int)

doubler 21",
                42
            );
        }

        [Test]
        public void UndeclaredRegistrationsStayUsable()
        {
            // a host may serve many scripts and register far more than any one of them uses
            TestConfigured(
                ctx =>
                {
                    ctx.RegisterProperty("half", () => 21);
                    ctx.RegisterProperty("unused", () => "nobody declared me");
                },
                @"
declare
    let half:int

half * 2",
                42
            );
        }

        [Test]
        public void MissingReferenceIsNotACompilationProblem()
        {
            // the host may well have registered the assembly by other means, so a reference that
            // does not resolve is a warning and the script still runs
            TestConfigured(
                ctx => { ctx.RegisterProperty("half", () => 21); },
                @"
declare
    reference ""./no/such/assembly.dll""
    let half:int

half * 2",
                42
            );
        }

        [Test]
        public void ReferencedAssemblyIsLoadedByTheCompilerToo()
        {
            // whatever the editor lets a script name, running it has to accept as well
            TestConfigured(
                ctx => { },
                @"
declare
    reference ""System.Net.Http""

use System.Net.Http
let http = new HttpClient ()
http.Timeout.TotalSeconds > 0.0",
                true
            );
        }

        [Test]
        public void DeclarationsFollowUseStatements()
        {
            TestConfigured(
                ctx => { ctx.RegisterProperty("half", () => 21); },
                @"
use System.Text

declare
    let half:int

half * 2",
                42
            );
        }

        #endregion

        #region Property checks

        [Test]
        public void PropertyDeclaredButNotRegistered()
        {
            TestErrorConfigured(
                ctx => { },
                @"
declare
    let half:int",
                CompilerMessages.DeclaredPropertyMissing
            );
        }

        [Test]
        public void PropertyTypeDoesNotMatch()
        {
            TestErrorConfigured(
                ctx => { ctx.RegisterProperty("half", () => 21); },
                @"
declare
    let half:string",
                CompilerMessages.DeclaredPropertyTypeMismatch
            );
        }

        [Test]
        public void PropertyDeclaredWiderThanItIs()
        {
            // matched exactly rather than by assignability: an editor told 'object' would offer
            // the members of the wrong type
            TestErrorConfigured(
                ctx => { ctx.RegisterProperty("name", () => "test"); },
                @"
declare
    let name:object",
                CompilerMessages.DeclaredPropertyTypeMismatch
            );
        }

        [Test]
        public void PropertyDeclaredAsVarIsReadonlyInTheHost()
        {
            TestErrorConfigured(
                ctx => { ctx.RegisterProperty("half", () => 21); },
                @"
declare
    var half:int",
                CompilerMessages.DeclaredPropertyNotWritable
            );
        }

        [Test]
        public void PropertyDeclaredTwice()
        {
            TestErrorConfigured(
                ctx => { ctx.RegisterProperty("half", () => 21); },
                @"
declare
    let half:int
    let half:int",
                CompilerMessages.DeclaredPropertyDuplicated
            );
        }

        [Test]
        public void LetNarrowsAWritableProperty()
        {
            // the declaration is the contract: an editor that has only the declaration and a
            // compiler that also has the host must agree on whether the assignment is legal
            var x = 0;
            TestErrorConfigured(
                ctx => { ctx.RegisterProperty("x", () => x, nx => x = nx); },
                @"
declare
    let x:int

x = 1",
                CompilerMessages.GlobalPropertyNoSetter
            );
        }

        [Test]
        public void VarKeepsAWritablePropertyWritable()
        {
            var x = 0;
            TestConfigured(
                ctx => { ctx.RegisterProperty("x", () => x, nx => x = nx); },
                @"
declare
    var x:int

x = 42",
                null
            );

            Assert.AreEqual(42, x);
        }

        #endregion

        #region Function checks

        [Test]
        public void FunctionDeclaredButNotRegistered()
        {
            TestErrorConfigured(
                ctx => { },
                @"
declare
    fun addNumbers:int (a:int b:int)",
                CompilerMessages.DeclaredFunctionMissing
            );
        }

        [Test]
        public void FunctionArgumentsDoNotMatch()
        {
            TestErrorConfigured(
                ctx => { ctx.RegisterFunction("addNumbers", typeof(ImportableStaticMethods).GetMethod("AddNumbers")); },
                @"
declare
    fun addNumbers:int (a:string b:string)",
                CompilerMessages.DeclaredFunctionSignatureMismatch
            );
        }

        [Test]
        public void FunctionReturnTypeDoesNotMatch()
        {
            TestErrorConfigured(
                ctx => { ctx.RegisterFunction("addNumbers", typeof(ImportableStaticMethods).GetMethod("AddNumbers")); },
                @"
declare
    fun addNumbers:string (a:int b:int)",
                CompilerMessages.DeclaredFunctionSignatureMismatch
            );
        }

        [Test]
        public void FunctionDeclaredTwiceWithTheSameSignature()
        {
            TestErrorConfigured(
                ctx => { ctx.RegisterFunction("addNumbers", typeof(ImportableStaticMethods).GetMethod("AddNumbers")); },
                @"
declare
    fun addNumbers:int (a:int b:int)
    fun addNumbers:int (a:int b:int)",
                CompilerMessages.DeclaredFunctionDuplicated
            );
        }

        #endregion

        #region Type alias checks

        [Test]
        public void TypeAliasDeclaredTwice()
        {
            TestErrorConfigured(
                ctx => { },
                @"
declare
    type Dt = System.DateTime
    type Dt = System.DateTime",
                CompilerMessages.DeclaredTypeDuplicated
            );
        }

        [Test]
        public void TypeAliasCollidesWithAScriptType()
        {
            TestErrorConfigured(
                ctx => { },
                @"
declare
    type Point = System.DateTime

record Point
    X : int",
                CompilerMessages.DeclaredTypeConflict
            );
        }

        [Test]
        public void TypeAliasDoesNotMatchTheHostRegistration()
        {
            TestErrorConfigured(
                ctx => { ctx.RegisterType("Dt", typeof(TimeSpan)); },
                @"
declare
    type Dt = System.DateTime",
                CompilerMessages.DeclaredTypeMismatch
            );
        }

        [Test]
        public void TypeAliasTargetCannotBeResolved()
        {
            TestErrorConfigured(
                ctx => { },
                @"
declare
    type Nope = No.Such.Type",
                CompilerMessages.TypeNotFound
            );
        }

        #endregion

        #region Placement and syntax

        [Test]
        public void DeclarationBlockAfterCode()
        {
            TestErrorConfigured(
                ctx => { ctx.RegisterProperty("half", () => 21); },
                @"
let a = 1

declare
    let half:int",
                CompilerMessages.DeclareBlockNotAtTop
            );
        }

        [Test]
        public void DeclarationBlockAfterAFunction()
        {
            TestErrorConfigured(
                ctx => { ctx.RegisterProperty("half", () => 21); },
                @"
fun test:int -> 1

declare
    let half:int",
                CompilerMessages.DeclareBlockNotAtTop
            );
        }

        [Test]
        public void PureIsNotAllowedInADeclaration()
        {
            TestError(
                @"
declare
    pure fun test:int (a:int)",
                ParserMessages.DeclarePureNotAllowed
            );
        }

        [Test]
        public void GenericFunctionCannotBeDeclared()
        {
            TestError(
                @"
declare
    fun test<T>:T (a:T)",
                ParserMessages.DeclareGenericNotAllowed
            );
        }

        [Test]
        public void DeclarationBlockMustBeIndented()
        {
            TestError(
                @"
declare
let half:int",
                ParserMessages.DeclareIndentExpected
            );
        }

        [Test]
        public void UnknownDeclarationKind()
        {
            TestError(
                @"
declare
    record Foo",
                ParserMessages.DeclarationExpected
            );
        }

        [Test]
        public void ReferencePathMustBeAString()
        {
            TestError(
                @"
declare
    reference FooBar.dll",
                ParserMessages.DeclareReferencePathExpected
            );
        }

        [Test]
        public void TypeAliasUsesAnEqualsSign()
        {
            TestError(
                @"
declare
    type Dt : System.DateTime",
                ParserMessages.SymbolExpected
            );
        }

        [Test]
        public void ReferenceIsAnOrdinaryIdentifierOutsideADeclareBlock()
        {
            Test(
                @"
let reference = 42
reference",
                42
            );
        }

        #endregion
    }
}
