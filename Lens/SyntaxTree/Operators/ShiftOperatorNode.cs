using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
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

		/// <summary>
		/// Extra hint for function composition.
		/// </summary>
		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var leftType = LeftOperand.Resolve(ctx);
			if (leftType.IsCallableType())
			{
				var mbr = RightOperand as GetMemberNode;
				if (mbr != null)
				{
					// function created from method name: add a type hint
					if (mbr.TypeHints == null || mbr.TypeHints.Count == 0)
					{
						var delegateType = ReflectionHelper.WrapDelegate(leftType);
						mbr.TypeHints = new List<TypeSignature> {delegateType.ReturnType.FullName};
					}
				}
			}

			return base.resolve(ctx, mustReturn);
		}

		public override NodeBase Expand(Context ctx, bool mustReturn)
		{
			var leftType = LeftOperand.Resolve(ctx, mustReturn);
			var rightType = RightOperand.Resolve(ctx, mustReturn);

			if (leftType.IsCallableType() && rightType.IsCallableType())
			{
				if (!ReflectionHelper.CanCombineDelegates(leftType, rightType))
					error(Translations.CompilerMessages.DelegatesNotCombinable, leftType, rightType);

				var leftVar = ctx.Unique.TempVariableName();
				var rightVar = ctx.Unique.TempVariableName();
				var delegateType = ReflectionHelper.WrapDelegate(leftType);
				var argDefs = delegateType.ArgumentTypes.Select(x => Expr.Arg(ctx.Unique.AnonymousArgName(), x.FullName)).ToArray();

				return Expr.Lambda(
					argDefs,
					Expr.Block(
						Expr.Let(leftVar, LeftOperand),
						Expr.Let(rightVar, RightOperand),
						Expr.Invoke(
							Expr.Get(rightVar),
							Expr.Invoke(
								Expr.Get(leftVar),
								argDefs.Select(x => Expr.Get(x.Name)).ToArray()
							)
						)
					)
				);
			}

			return base.Expand(ctx, mustReturn);
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			if (leftType.IsAnyOf(typeof (int), typeof (long)) && rightType == typeof (int))
				return leftType;

			if (!IsLeft && ReflectionHelper.CanCombineDelegates(leftType, rightType))
				return ReflectionHelper.CombineDelegates(leftType, rightType);

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
