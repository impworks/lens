using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the boolean inversion operator.
	/// </summary>
	public class InversionOperatorNode : UnaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "not"; }
		}

		public override Type GetExpressionType(Context ctx)
		{
			var type = Operand.GetExpressionType(ctx);

			if(type != typeof(bool))
				TypeError(type);

			return type;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}
	}
}
