﻿using System;
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

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var left = LeftOperand.GetExpressionType(ctx);
			var right = RightOperand.GetExpressionType(ctx);

			if(!CastOperatorNode.IsImplicitlyBoolean(left) || !CastOperatorNode.IsImplicitlyBoolean(right))
				TypeError(left, right);

			return typeof (bool);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			// validate nodes
			GetExpressionType(ctx);

			CastOperatorNode.CompileAsBoolean(LeftOperand, ctx);
			CastOperatorNode.CompileAsBoolean(RightOperand, ctx);

			if(Kind == BooleanOperatorKind.And)
				gen.EmitAnd();
			else if (Kind == BooleanOperatorKind.Or)
				gen.EmitOr();
			else if (Kind == BooleanOperatorKind.Xor)
				gen.EmitXor();
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
