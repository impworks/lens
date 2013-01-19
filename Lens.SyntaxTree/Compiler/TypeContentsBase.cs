namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// The base class of a type-contained entity.
	/// </summary>
	internal abstract class TypeContentsBase
	{
		/// <summary>
		/// The name of the current entity.
		/// </summary>
		public string Name { get; protected set; }

		/// <summary>
		/// The type that contains current entity.
		/// </summary>
		public TypeEntity ContainerType { get; protected set; }

		/// <summary>
		/// Creates the assembly instances for the current entity.
		/// </summary>
		public abstract void PrepareSelf(Context ctx);
	}
}
