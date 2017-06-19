using System.Collections.Generic;
using System.Reflection;
using Lens.Test.Internals;
using Lens.Translations;
using NUnit.Framework;

namespace Lens.Test.Features
{
    [TestFixture]
    internal class ImportingTest : TestBase
    {
        [Test]
        public void ImportVoidFunction()
        {
            var method = typeof(ImportableStaticMethods).GetMethod("DoNothing");
            Assert.IsNotNull(method);

            TestConfigured(
                ctx => { ctx.RegisterFunction("doNothing", method); },
                "doNothing()",
                null
            );
        }

        [Test]
        public void ImportFunctionWithArgs()
        {
            var method = typeof(ImportableStaticMethods).GetMethod("AddNumbers");
            Assert.IsNotNull(method);

            TestConfigured(
                ctx => { ctx.RegisterFunction("addNumbers", method); },
                "addNumbers 1 2",
                3
            );
        }

        [Test]
        public void ImportFunctionWithParams()
        {
            var method = typeof(ImportableStaticMethods).GetMethod("Sum");
            Assert.IsNotNull(method);

            TestConfigured(
                ctx => { ctx.RegisterFunction("sum", method); },
                "sum 1 2 3",
                6
            );
        }

        [Test]
        public void ImportGenericFunctionWithLambda1()
        {
            var method = typeof(ImportableStaticMethods).GetMethod("Project");
            Assert.IsNotNull(method);

            TestConfigured(
                ctx => { ctx.RegisterFunction("project", method); },
                @"
project
  <| new [1; 2; 3; 4; 5]
  <| x -> x*x
",
                new List<int> {1, 4, 9, 16, 25}
            );
        }

        [Test]
        public void ImportGenericFunctionWithLambda2()
        {
            var method = typeof(ImportableStaticMethods).GetMethod("Project");
            Assert.IsNotNull(method);

            TestConfigured(
                ctx => { ctx.RegisterFunction("project", method); },
                "new [1; 2; 3; 4; 5].project (x -> x*x)",
                new List<int> {1, 4, 9, 16, 25}
            );
        }

        [Test]
        public void ImportAllOverloads()
        {
            TestConfigured(
                ctx => { ctx.RegisterFunctionOverloads(typeof(ImportableStaticMethods), nameof(ImportableStaticMethods.OverloadedAdd), "myAdd"); },
                @"
new [
    myAdd 1.3 3.7
    myAdd 1 2 3
    myAdd ""hello"" ""world""
]
",
                new object[] {5.0, 6.0, "helloworld"}
            );
        }

        [Test]
        public void ImportClass()
        {
            TestConfigured(
                ctx => { ctx.RegisterType(typeof(ImportableClass)); },
                @"
let x = new ImportableClass (""a"" + ""b"")
x.Value
",
                "ab"
            );
        }

        [Test]
        public void ImportClassWithRename()
        {
            TestConfigured(
                ctx => { ctx.RegisterType("WtfClass", typeof(ImportableClass)); },
                @"
let x = new WtfClass (""a"" + ""b"")
x.Value
",
                "ab"
            );
        }

        [Test]
        public void ImportInstanceMethodError()
        {
            var method = typeof(ImportableStaticMethods).GetMethod("UnimportableMethod");
            Assert.IsNotNull(method);

            TestErrorConfigured(
                ctx => { ctx.RegisterFunction("wtf", method); },
                "wtf ()",
                CompilerMessages.ImportUnsupportedMethod
            );
        }

        [Test]
        public void ImportPrivateMethodError()
        {
            var method = typeof(ImportableStaticMethods).GetMethod("UnimportableMethod2", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);

            TestErrorConfigured(
                ctx => { ctx.RegisterFunction("wtf", method); },
                "wtf ()",
                CompilerMessages.ImportUnsupportedMethod
            );
        }

        [Test]
        public void ImportNoOverloadsError()
        {
            TestErrorConfigured(
                ctx => { ctx.RegisterFunctionOverloads(typeof(ImportableStaticMethods), "NonExistantMethodName"); },
                "()",
                CompilerMessages.NoOverloads
            );
        }
    }
}