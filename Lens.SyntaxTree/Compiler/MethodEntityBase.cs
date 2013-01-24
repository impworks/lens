using System.Collections.Generic;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// The base entity for a method and a constructor that allows lookup by argument types.
	/// </summary>
	abstract class MethodEntityBase : TypeContentsBase
	{
		protected MethodEntityBase()
		{
			Arguments = new Dictionary<string, FunctionArgument>();
			Scope = new Scope();
		}

		/// <summary>
		/// The argument list.
		/// </summary>
		public readonly Dictionary<string, FunctionArgument> Arguments;

		/// <summary>
		/// Lexical scope of the current method.
		/// </summary>
		public readonly Scope Scope;

		/// <summary>
		/// Process closures.
		/// </summary>
		public void ProcessClosures(Context ctx)
		{
			
		}
	}
}
