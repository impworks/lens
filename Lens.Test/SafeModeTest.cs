using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Translations;
using NUnit.Framework;

namespace Lens.Test
{
	[TestFixture]
	public class SafeModeTest : TestBase
	{
		[Test]
		public void BlacklistNamespaces1()
		{
			var opts = new LensCompilerOptions
			{
				SafeMode = SafeMode.Blacklist,
				SafeModeExplicitNamespaces = new List<string> { "System.Text" }
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
				SafeModeExplicitNamespaces = new List<string> { "System.Text" }
			};

			var src = @"
using System.Text.RegularExpressions
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
				SafeModeExplicitTypes = new List<string> { "System.Collections.Stack" }
			};

			var src = @"
using System.Collections
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
				SafeModeExplicitTypes = new List<string> { "System.GC" }
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
	}
}
