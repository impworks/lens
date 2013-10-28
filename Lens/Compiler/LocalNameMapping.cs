namespace Lens.Compiler
{
	/// <summary>
	/// Declares a way of handling a local variable.
	/// </summary>
	internal enum LocalNameMapping
	{
		/// <summary>
		/// Local variable is ordinary and is represented by a LocalBuilder.
		/// </summary>
		Default,

		/// <summary>
		/// Local variable is closured and is mapped to a field of a special object.
		/// </summary>
		Closure,

		/// <summary>
		/// Local variable is mapped to a field of current object.
		/// Is used to maintain arguments in iterator methods.
		/// </summary>
		Field
	}
}
