using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lens.Compiler;
using Lens.Translations;
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

		[Test]
		public void BlacklistEnvironment1()
		{
			var src = @"GC::Collect ()";
			testSubsystem(typeof(GC), SafeModeSubsystem.Environment, src);
		}

		[Test]
		public void BlacklistEnvironment2()
		{
			var src = @"Environment::StackTrace";
			testSubsystem(typeof(Environment), SafeModeSubsystem.Environment, src);
		}

		[Test]
		public void BlacklistEnvironment3()
		{
			var src = @"AppDomain::CurrentDomain.IsFullyTrusted";
			testSubsystem(typeof(AppDomain), SafeModeSubsystem.Environment, src);
		}

		[Test]
		public void BlacklistEnvironment4()
		{
			var src = @"System.Diagnostics.Debug::WriteLine ""test""";
			testSubsystem(typeof(System.Diagnostics.Debug), SafeModeSubsystem.Environment, src);
		}
		
		[Test]
		public void BlacklistEnvironment5()
		{
			var src = @"System.Runtime.InteropServices.Marshal::IsComObject (new object ())";
			testSubsystem(typeof(System.Runtime.InteropServices.Marshal), SafeModeSubsystem.Environment, src);
		}

		[Test]
		public void BlacklistIO1()
		{
			var src = @"System.IO.Directory::Exists ""C:\\A\\B""";
			testSubsystem(typeof(System.IO.Directory), SafeModeSubsystem.IO, src);
		}

		[Test]
		public void BlacklistIO2()
		{
			var src = @"System.IO.IsolatedStorage.IsolatedStorageFile::IsEnabled";
			testSubsystem(typeof(System.IO.IsolatedStorage.IsolatedStorageFile), SafeModeSubsystem.IO, src);
		}

		[Test]
		public void BlacklistThreading1()
		{
			var src = @"
var workThreads = 0
var cpThreads = 0
System.Threading.ThreadPool::GetAvailableThreads (ref workThreads) (ref cpThreads)";
			testSubsystem(typeof(System.Threading.ThreadPool), SafeModeSubsystem.Threading, src);
		}

		[Test]
		public void BlacklistThreading2()
		{
			var src = @"System.Threading.Tasks.Task::Run (-> print ""hello world!"")";
			testSubsystem(typeof(System.Threading.Tasks.Task), SafeModeSubsystem.Threading, src);
		}

		[Test]
		public void BlacklistReflection1()
		{
			var src = @"System.Reflection.Assembly::GetCallingAssembly()";
			testSubsystem(typeof(System.Reflection.Assembly), SafeModeSubsystem.Reflection, src);
		}

		[Test]
		public void BlacklistReflection2()
		{
			var src = @"System.AppDomain::CurrentDomain.IsFullyTrusted";
			testSubsystem(typeof(AppDomain), SafeModeSubsystem.Reflection, src);
		}

		[Test]
		public void BlacklistReflection3()
		{
			var src = @"(typeof int).Fullname";
			testSubsystem(typeof(Type), SafeModeSubsystem.Reflection, src);
		}

		[Test]
		public void BlacklistNetwork1()
		{
			var src = @"new System.Net.HttpListener ()";
			testSubsystem(typeof(System.Net.HttpListener), SafeModeSubsystem.Network, src);
		}

		[Test]
		public void BlacklistNetwork2()
		{
			var src = @"System.Net.Sockets.Socket::OSSupportsIPv4";
			testSubsystem(typeof(System.Net.Sockets.Socket), SafeModeSubsystem.Network, src);
		}

		private void testSubsystem(Type type, SafeModeSubsystem system, string code)
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
