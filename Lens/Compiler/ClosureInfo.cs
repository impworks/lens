using System.Reflection.Emit;
using Lens.Compiler.Entities;

namespace Lens.Compiler
{
	internal class ClosureInfo
	{
		/// <summary>
		/// The type entity that represents current closure.
		/// </summary>
		public TypeEntity ClosureType;

		/// <summary>
		/// The local variable in which the closure is saved.
		/// </summary>
		public LocalBuilder ClosureVariable;

		/// <summary>
		/// Parent closure for lookup purposes.
		/// </summary>
		public ClosureInfo ParentClosure;

		/// <summary>
		/// Flag indicating that some of current scope's expressions reference variables from outer scope.
		/// </summary>
		public bool ReferencesParent;
	}
}
