using System.Collections.Generic;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// The base entity for a method and a constructor that allows lookup by argument types.
	/// </summary>
	abstract class MethodEntityBase : TypeContentsBase
	{
		/// <summary>
		/// The argument list.
		/// </summary>
		public Dictionary<string, FunctionArgument> Arguments { get; set; }
	}
}
