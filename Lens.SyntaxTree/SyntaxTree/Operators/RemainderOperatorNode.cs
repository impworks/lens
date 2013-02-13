using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that divides one value by another value.
	/// </summary>
	public class RemainderOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "%"; }
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return getNumericTypeOrError(ctx);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}
	}
}
