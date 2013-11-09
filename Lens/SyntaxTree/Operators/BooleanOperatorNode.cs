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

		/// <summary>
		/// The kind of boolean operator.
		/// </summary>
		public LogicalOperatorKind Kind { get; set; }

		protected override bool IsNumericOperator
		{
			get { return Kind == LogicalOperatorKind.Xor; }
		}

		public override string OperatorRepresentation
		{
			get
			{
				return Kind == LogicalOperatorKind.And ? "&&" : (Kind == LogicalOperatorKind.Or ? "||" : "^^");
			}
		}

		public override string OverloadedMethodName
		{
			get { return Kind == LogicalOperatorKind.Xor ? "op_ExclusiveOr" : null; }
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return CastOperatorNode.IsImplicitlyBoolean(leftType) && CastOperatorNode.IsImplicitlyBoolean(rightType)
				       ? typeof (bool)
				       : null;
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			// validate nodes
			GetExpressionType(ctx);

			if (Kind == LogicalOperatorKind.And)
			{
				var cond = Expr.If(LeftOperand, Expr.Block(RightOperand), Expr.Block(Expr.False()));
				cond.Compile(ctx, true);
			}
			else if (Kind == LogicalOperatorKind.Or)
			{
				var cond = Expr.If(LeftOperand, Expr.Block(Expr.True()), Expr.Block(RightOperand));
				cond.Compile(ctx, true);
			}
			else if (Kind == LogicalOperatorKind.Xor)
			{
				LeftOperand.Compile(ctx, true);
				RightOperand.Compile(ctx, true);
				gen.EmitXor();
			}
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return Kind == LogicalOperatorKind.And ? left && right : (Kind == LogicalOperatorKind.Or ? left || right : left ^ right);
		}
	}

	/// <summary>
	/// The kind of boolean operators.
	/// </summary>
	public enum LogicalOperatorKind
	{
		And,
		Or,
		Xor
	}
}
