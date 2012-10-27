using System.Collections.Generic;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A base class for various forms of method invocation that stores arguments.
	/// </summary>
	abstract public class InvocationNodeBase : NodeBase
	{
		protected InvocationNodeBase()
		{
			Arguments = new List<NodeBase>();
		}

		public List<NodeBase> Arguments;
	}
}
