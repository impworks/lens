using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the operator that returns a default value for the type.
	/// </summary>
	public class DefaultOperatorNode : TypeOperatorNodeBase
	{
		public DefaultOperatorNode(string type = null)
		{
			Type = type;
		}

		public override Type GetExpressionType(Context ctx)
		{
			return ctx.ResolveType(Type.Signature);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			return string.Format("default({0})", Type);
		}
	}
}
