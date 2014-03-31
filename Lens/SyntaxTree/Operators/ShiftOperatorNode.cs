using System;
using System.Linq;
using Lens.Compiler;
using Lens.SyntaxTree.Expressions;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators
{
	internal class ShiftOperatorNode : BinaryOperatorNodeBase
	{
		public bool IsLeft { get; set; }

		protected override string OperatorRepresentation
		{
			get { return IsLeft ? "<:" : ":>"; }
		}

		protected override string OverloadedMethodName
		{
			get { return IsLeft ? "op_LeftShift" : "op_RightShift"; }
		}

		public override NodeBase Expand(Context ctx, bool mustReturn)
		{
			var leftType = LeftOperand.Resolve(ctx, mustReturn);
			var rightType = RightOperand.Resolve(ctx, mustReturn);

			if (leftType.IsCallableType() && rightType.IsCallableType())
			{
				if (!ctx.CanCombineDelegates(leftType, rightType))
					error(Translations.CompilerMessages.DelegatesNotCombinable, leftType, rightType);

				NodeBase body;
				if (RightOperand is InvocationNode)
				{
					(RightOperand as InvocationNode).Arguments.Add(LeftOperand);
					body = RightOperand;
				}
				else
				{
					body = Expr.Invoke(RightOperand, LeftOperand);
				}

				var leftMethod = ctx.WrapDelegate(leftType);
				return Expr.Lambda(
					leftMethod.ArgumentTypes.Select(x => Expr.Arg(ctx.Unique.AnonymousArgName(), x.FullName)),
					body
				);
			}

			return base.Expand(ctx, mustReturn);
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			if (leftType.IsAnyOf(typeof (int), typeof (long)) && rightType == typeof (int))
				return leftType;

			if (!IsLeft && ctx.CanCombineDelegates(leftType, rightType))
				return ctx.CombineDelegates(leftType, rightType);

			return null;
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentMethod.Generator;

			LeftOperand.Emit(ctx, true);
			RightOperand.Emit(ctx, true);

			gen.EmitShift(IsLeft);
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return IsLeft ? left << right : left >> right;
		}
	}
}
