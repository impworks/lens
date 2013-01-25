using System.Collections.Generic;

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
			ScopeManager = new ScopeManager();
		}

		/// <summary>
		/// The argument list.
		/// </summary>
		public readonly Dictionary<string, FunctionArgument> Arguments;

		/// <summary>
		/// Lexical scope of the current method.
		/// </summary>
		public readonly ScopeManager ScopeManager;

		/// <summary>
		/// Process closures.
		/// </summary>
		public void ProcessClosures(Context ctx)
		{
			
		}
	}
}
