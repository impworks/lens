using System.Collections.Generic;
using Lens.SyntaxTree;
using NUnit.Framework;

namespace Lens.Test
{
	public class TestBase
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

		protected static void Test(string src, object value, LensCompilerOptions opts)
		{
			Assert.AreEqual(value, Compile(src, opts));
		}

		protected static void Test(IEnumerable<NodeBase> nodes, object value, LensCompilerOptions opts)
		{
			Assert.AreEqual(value, Compile(nodes, opts));
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
	}
}
