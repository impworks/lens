using System.Collections.Generic;
using Lens.SyntaxTree.Compiler.SafeMode;

namespace Lens.SyntaxTree
{
	/// <summary>
	/// A list of options to tweak the compiler.
	/// </summary>
	public class LensCompilerOptions
	{
		/// <summary>
		/// Checks whether the generated assembly can be saved to disk.
		/// Default = false.
		/// </summary>
		public bool AllowSave = false;

		/// <summary>
		/// Checks whether the compiler should auto-include a bunch of common namespaces and assemblies.
		/// Default = true.
		/// </summary>
		public bool UseDefaultNamespaces = true;

		/// <summary>
		/// Checks whether extension methods are allowed. Can be disabled to speed up compilation.
		/// Default = true.
		/// </summary>
		public bool AllowExtensionMethods = true;

		/// <summary>
		/// Checks whether LENS standard library should be registered.
		/// Default = true.
		/// </summary>
		public bool LoadStandardLibrary = true;

		/// <summary>
		/// Checks whether the generated assembly must be saved as a console executable.
		/// Depends on AllowSave.
		/// Default = false.
		/// </summary>
		public bool SaveAsExe = false;

		/// <summary>
		/// Specifies the file name for generated assembly.
		/// Depends on AllowSave.
		/// Default = none.
		/// </summary>
		public string FileName = string.Empty;

		/// <summary>
		/// Checks if operations on constants must be performed at compile time.
		/// Default = true.
		/// </summary>
		public bool UnrollConstants = true;

		/// <summary>
		/// Checks whether the script should be compiled in a sandbox environment.
		/// Default = Disabled
		/// </summary>
		public SafeMode SafeMode = SafeMode.Disabled;

		/// <summary>
		/// The list of types that form a blacklist or a whitelist depending on safe mode.
		/// </summary>
		public List<string> SafeModeExplicitTypes = new List<string>();

		/// <summary>
		/// The list of namespaces that form a blacklist or a whitelist depending on safe mode.
		/// </summary>
		public List<string> SafeModeExplicitNamespaces = new List<string>();

		/// <summary>
		/// The whitelisted or blacklisted subsystems for safe mode (networking, IO, etc).
		/// </summary>
		public SafeModeSubsystem SafeModeExplicitSubsystems = SafeModeSubsystem.None;
	}
}
