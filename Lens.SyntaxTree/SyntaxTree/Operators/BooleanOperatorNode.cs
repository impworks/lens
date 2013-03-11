using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing AND, OR or XOR binary operations.
	/// </summary>
	public class BooleanOperatorNode : BinaryOperatorNodeBase
	{
		public BooleanOperatorNode(BooleanOperatorKind kind = default(BooleanOperatorKind))
		{
			Kind = kind;
		}

		/// <summary>
		/// The kind of boolean operator.
		/// </summary>
		public BooleanOperatorKind Kind { get; set; }

		public override string OperatorRepresentation
		{
			get
			{
				switch (Kind)
				{
					case BooleanOperatorKind.And: return "&&";
					case BooleanOperatorKind.Or:  return "||";
					case BooleanOperatorKind.Xor: return "^^";

					default: throw new ArgumentException("Boolean operator kind is invalid!");
				}
			}
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return CastOperatorNode.IsImplicitlyBoolean(leftType) && CastOperatorNode.IsImplicitlyBoolean(rightType)
				       ? typeof (bool)
				       : null;
		}

		protected override void  compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			// validate nodes
			GetExpressionType(ctx);

			if (Kind == BooleanOperatorKind.And)
			{
				var cond = Expr.If(LeftOperand, Expr.Block(RightOperand), Expr.Block(Expr.False()));
				cond.Compile(ctx, true);
			}
			else if (Kind == BooleanOperatorKind.Or)
			{
				var cond = Expr.If(LeftOperand, Expr.Block(Expr.True()), Expr.Block(RightOperand));
				cond.Compile(ctx, true);
			}
			else if (Kind == BooleanOperatorKind.Xor)
			{
				LeftOperand.Compile(ctx, true);
				RightOperand.Compile(ctx, true);
				gen.EmitXor();
			}
		}
	}

	/// <summary>
	/// The kind of boolean operators.
	/// </summary>
	public enum BooleanOperatorKind
	{
		And,
		Or,
		Xor
	}
}
