using System;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators.Binary
{
	/// <summary>
	/// A node representing AND / OR binary operations.
	/// </summary>
	internal class BooleanOperatorNode : BinaryOperatorNodeBase
	{
		#region Constructor

		public BooleanOperatorNode(LogicalOperatorKind kind = default(LogicalOperatorKind))
		{
            if(kind == LogicalOperatorKind.Xor)
                throw new ArgumentException("Use XorOperatorNode to represent a XOR ");

			Kind = kind;
		}

		#endregion

		#region Fields

		public LogicalOperatorKind Kind;

		#endregion

		#region Operator basics

		protected override bool IsNumericOperator => false;

	    protected override string OperatorRepresentation => Kind == LogicalOperatorKind.And ? "&&" : "||";

	    protected override BinaryOperatorNodeBase RecreateSelfWithArgs(NodeBase left, NodeBase right)
        {
            return new BooleanOperatorNode(Kind) { LeftOperand = left, RightOperand = right };
        }

		#endregion

		#region Resolve

		protected override Type ResolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType.IsImplicitlyBoolean() && rightType.IsImplicitlyBoolean()
				       ? typeof (bool)
				       : null;
		}

		#endregion

		#region Expand

		protected override NodeBase Expand(Context ctx, bool mustReturn)
		{
			if (!IsConstant)
			{
				return Kind == LogicalOperatorKind.And
					? Expr.If(LeftOperand, Expr.Block(Expr.Cast<bool>(RightOperand)), Expr.Block(Expr.False()))
					: Expr.If(LeftOperand, Expr.Block(Expr.True()), Expr.Block(Expr.Cast<bool>(RightOperand)));
			}

			return base.Expand(ctx, mustReturn);
		}

		protected override void EmitOperator(Context ctx)
		{
			throw new InvalidOperationException("The BooleanOperatorNode has not been expanded!");
		}

		#endregion

		#region Constant unroll

		protected override dynamic UnrollConstant(dynamic left, dynamic right)
		{
			return Kind == LogicalOperatorKind.And ? left && right : left || right;
		}

		#endregion
	}

	/// <summary>
	/// The kind of bit or boolean operators.
	/// </summary>
	public enum LogicalOperatorKind
	{
		And,
		Or,
        Xor
	}
}
