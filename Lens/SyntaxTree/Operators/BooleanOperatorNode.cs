using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing AND, OR or XOR binary operations.
	/// </summary>
	internal class BooleanOperatorNode : BinaryOperatorNodeBase
	{
		public BooleanOperatorNode(LogicalOperatorKind kind = default(LogicalOperatorKind))
		{
			Kind = kind;
		}

		public LogicalOperatorKind Kind { get; set; }

		protected override bool IsNumericOperator { get { return false; } }

		protected override string OperatorRepresentation
		{
			get { return Kind == LogicalOperatorKind.And ? "&&" : "||"; }
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return CastOperatorNode.IsImplicitlyBoolean(leftType) && CastOperatorNode.IsImplicitlyBoolean(rightType)
				       ? typeof (bool)
				       : null;
		}

		protected override NodeBase expand(Context ctx, bool mustReturn)
		{
			if (!IsConstant)
			{
				return Kind == LogicalOperatorKind.And
					? Expr.If(LeftOperand, Expr.Block(Expr.Cast<bool>(RightOperand)), Expr.Block(Expr.False()))
					: Expr.If(LeftOperand, Expr.Block(Expr.True()), Expr.Block(Expr.Cast<bool>(RightOperand)));
			}

			return base.expand(ctx, mustReturn);
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return Kind == LogicalOperatorKind.And ? left && right : left || right;
		}

		protected override void emitOperator(Context ctx)
		{
			throw new InvalidOperationException("The BooleanOperatorNode has not been expanded!");
		}
	}

	/// <summary>
	/// The kind of boolean operators.
	/// </summary>
	public enum LogicalOperatorKind
	{
		And,
		Or
	}
}
