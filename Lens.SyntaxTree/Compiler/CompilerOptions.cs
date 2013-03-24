namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// A list of options to tweak the compiler.
	/// </summary>
	public class CompilerOptions
	{
		/// <summary>
		/// Checks whether the generated assembly can be saved to disk.
		/// Default is false.
		/// </summary>
		public bool AllowSave = false;

		/// <summary>
		/// Checks whether the compiler should auto-include a bunch of common namespaces and assemblies.
		/// Default is true.
		/// </summary>
		public bool UseDefaultNamespaces = true;

		/// <summary>
		/// Checks whether extension methods are allowed. Can be disabled to speed up compilation.
		/// Default is true.
		/// </summary>
		public bool AllowExtensionMethods = true;
	}
}
