using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
			Assert.AreEqual(value, Compile(src, new LensCompilerOptions { UnrollConstants = true, AllowSave = true }));
			if (testConstants)
				Assert.AreEqual(value, Compile(src));
		}

		protected static void Test(IEnumerable<NodeBase> nodes, object value, bool testConstants = false)
		{
			Assert.AreEqual(value, Compile(nodes, new LensCompilerOptions {UnrollConstants = true}));
			if (testConstants)
				Assert.AreEqual(value, Compile(nodes));
		}

		protected static void TestError(string src, string bareMsg)
		{
			var ex = Assert.Throws<LensCompilerException>(() => Compile(src));

			if (!bareMsg.Contains('{'))
			{
				Assert.AreEqual(bareMsg, ex.Message);
			}
			else
			{
				var regex = new Regex(regexFromMsg(bareMsg));
				Assert.IsTrue(regex.IsMatch(ex.Message));
			}
		}

		protected static void Test(string src, object value, LensCompilerOptions opts)
		{
			Assert.AreEqual(value, Compile(src, opts));
		}

		protected static void Test(IEnumerable<NodeBase> nodes, object value, LensCompilerOptions opts)
		{
			Assert.AreEqual(value, Compile(nodes, opts));
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
			opts = opts ?? new LensCompilerOptions { AllowSave = true };
			return new LensCompiler(opts).Run(src);
		}

		protected static object Compile(IEnumerable<NodeBase> nodes, LensCompilerOptions opts = null)
		{
			opts = opts ?? new LensCompilerOptions { AllowSave = true };
			return new LensCompiler(opts).Run(nodes);
		}

		private static Regex _MessageWildcardRegex = new Regex(@"\{[0-9]\}", RegexOptions.Compiled);
		private static Regex _MessageSymbolRegex = new Regex(@"[\-\[\]\/\(\)\*\+\?\.\\\^\$\|]", RegexOptions.Compiled);

		private static string regexFromMsg(string msg)
		{
			msg = _MessageSymbolRegex.Replace(msg, @"\$1");
			msg = _MessageWildcardRegex.Replace(msg, "(.+)");
			return "$" + msg + "^";
		}
	}
}
