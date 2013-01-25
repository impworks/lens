using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that subtracts a value from another value.
	/// </summary>
	public class SubtractOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "-"; }
		}

		protected override Type resolveExpressionType(Context ctx)
		{
			return getNumericTypeOrError(ctx);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}
	}
}
