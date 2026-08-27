using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Lexer;
using Lens.Parser;
using Lens.SyntaxTree;
using NUnit.Framework;

namespace Lens.Test
{
    internal class TestBase
    {
        protected static void Test(string src, object value, bool testConstants = false)
        {
            var opts = new LensCompilerOptions
            {
                UnrollConstants = true,
                #if NET_CLASSIC
                AllowSave = true
                #endif
            };
            Assert.AreEqual(value, Compile(src, opts));
            if (testConstants)
                Assert.AreEqual(value, Compile(src));
        }

        protected static void Test(IEnumerable<NodeBase> nodes, object value, bool testConstants = false)
        {
            Assert.AreEqual(value, Compile(nodes, new LensCompilerOptions {UnrollConstants = true}));
            if (testConstants)
                Assert.AreEqual(value, Compile(nodes));
        }

        protected static void TestError(string src, string msg)
        {
            var exception = Assert.Throws<LensCompilerException>(() => Compile(src));
            var srcId = exception.Message.Substring(0, 6);
            var msgId = msg.Substring(0, 6);

            Assert.IsTrue(
                srcId == msgId,
                "Message does not match!\nExpected: {0}\nActual: {1}!",
                msg,
                exception.Message
            );
        }

        /// <summary>
        /// Checks that a script reports an error, and that the error is bound to the expected
        /// segment of the source. Lines and offsets are 1-based, as the lexer counts them.
        /// </summary>
        protected static void TestErrorAt(string src, string msg, int startLine, int startOffset, int endLine, int endOffset)
        {
            var exception = Assert.Throws<LensCompilerException>(() => Compile(src));

            Assert.AreEqual(
                msg.Substring(0, 6),
                exception.Message.Substring(0, 6),
                "Message does not match!\nExpected: {0}\nActual: {1}!",
                msg,
                exception.Message
            );

            Assert.AreEqual($"{startLine}:{startOffset}", exception.StartLocation?.ToString(), "Error starts in the wrong place!");
            Assert.AreEqual($"{endLine}:{endOffset}", exception.EndLocation?.ToString(), "Error ends in the wrong place!");
        }

        /// <summary>
        /// Checks that a script reports exactly the given list of diagnostics, in order.
        /// </summary>
        protected static void TestErrors(string src, params string[] msgs)
        {
            var compiler = CreateCompiler(null);
            Assert.Throws<LensCompilerException>(() => compiler.Compile(src));

            var actual = compiler.Diagnostics.Select(x => x.Message).ToArray();

            Assert.AreEqual(
                msgs.Select(x => x.Substring(0, 6)).ToArray(),
                actual.Select(x => x.Substring(0, 6)).ToArray(),
                "Diagnostics do not match!\nExpected: {0}\nActual: {1}",
                string.Join(", ", msgs),
                string.Join(", ", actual)
            );
        }

        protected static void Test(string src, object value, LensCompilerOptions opts)
        {
            Assert.AreEqual(value, Compile(src, opts));
        }

        protected static void Test(IEnumerable<NodeBase> nodes, object value, LensCompilerOptions opts)
        {
            Assert.AreEqual(value, Compile(nodes, opts));
        }

        protected void TestConfigured(Action<LensCompiler> setup, string src, object value)
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            setup(compiler);

            var actualValue = compiler.Run(src);
            Assert.AreEqual(value, actualValue);
        }

        protected void TestErrorConfigured(Action<LensCompiler> setup, string src, string msg)
        {
            var compiler = CreateCompiler(new LensCompilerOptions());
            var exception = Assert.Throws<LensCompilerException>(() =>
            {
                setup(compiler);
                compiler.Compile(src);
            });

            var srcId = exception.Message.Substring(0, 6);
            var msgId = msg.Substring(0, 6);

            Assert.IsTrue(
                srcId == msgId,
                "Message does not match!\nExpected: {0}\nActual: {1}!",
                msg,
                exception.Message
            );
        }

        protected static void TestParser(string source, params NodeBase[] expected)
        {
            Assert.AreEqual(expected, Parse(source).ToArray());
        }

        protected static IEnumerable<NodeBase> Parse(string source)
        {
            var lexer = new LensLexer(source);
            var parser = new LensParser(lexer.Lexems);
            return parser.Nodes;
        }

        protected static object Compile(string src, LensCompilerOptions opts = null)
        {
            return CreateCompiler(opts).Run(src);
        }

        protected static object Compile(IEnumerable<NodeBase> nodes, LensCompilerOptions opts = null)
        {
            return CreateCompiler(opts).Run(nodes);
        }

        protected static LensCompiler CreateCompiler(LensCompilerOptions opts)
        {
            var options = opts ?? new LensCompilerOptions
            {
                #if NET_CLASSIC
                AllowSave = true
                #endif
            };

            // running the whole suite through the lowering pass is how it is checked to preserve
            // behaviour on code that has nothing to do with state machines:
            //   LENS_LOWER_ALL=1 dotnet test
            if (Environment.GetEnvironmentVariable("LENS_LOWER_ALL") == "1")
                options.LowerAllFunctions = true;

            // and running it with symbols is how the debuggable backend is checked to compile the
            // same language: on .NET it is a different kind of assembly builder, so every script
            // the suite knows is a script it has to build too.
            //   LENS_DEBUG_ALL=1 dotnet test
            // Constants are not unrolled when debugging, so the handful of tests that assert on
            // what folding produces are expected to differ under this switch.
            if (Environment.GetEnvironmentVariable("LENS_DEBUG_ALL") == "1")
                options.DebugSettings.Enabled = true;

            var compiler = new LensCompiler(options);
            compiler.RegisterAssembly(Assembly.Load("System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"));
            return compiler;
        }
    }
}