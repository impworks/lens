using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the typeof operator.
	/// </summary>
	public class TypeofOperatorNode : TypeOperatorNodeBase
	{
		public TypeofOperatorNode(string type = null)
		{
			Type = type;
		}

		public override Type GetExpressionType(Context ctx)
		{
			return typeof (Type);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			return string.Format("typeof({0})", Type);
		}
	}
}
