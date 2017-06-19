using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators.Binary
{
	internal class ShiftOperatorNode : BinaryOperatorNodeBase
	{
		#region Fields

		/// <summary>
		/// Indicates the direction of the shift: left or right.
		/// </summary>
		public bool IsLeft;

		#endregion

		#region Operator basics

		protected override string OperatorRepresentation => IsLeft ? "<:" : ":>";

	    protected override string OverloadedMethodName => IsLeft ? "op_LeftShift" : "op_RightShift";

	    protected override BinaryOperatorNodeBase RecreateSelfWithArgs(NodeBase left, NodeBase right)
        {
            return new ShiftOperatorNode { IsLeft = IsLeft, LeftOperand = left, RightOperand = right };
        }

		#endregion

		#region Resolve

		/// <summary>
		/// Extra hint for function composition.
		/// </summary>
		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var leftType = LeftOperand.Resolve(ctx);

			// check if the shift operator is used for function composition
			if (leftType.IsCallableType())
			{
				// add left operand's return type as hint to right operand
				var leftDelegate = ReflectionHelper.WrapDelegate(leftType);
				if (RightOperand is GetMemberNode)
				{
					var mbr = RightOperand as GetMemberNode;
					if (mbr.TypeHints == null || mbr.TypeHints.Count == 0)
						mbr.TypeHints = new List<TypeSignature> {leftDelegate.ReturnType.FullName};
				}
				else if(RightOperand is LambdaNode)
				{
					var lambda = RightOperand as LambdaNode;
					lambda.Resolve(ctx);
					if (lambda.MustInferArgTypes)
						lambda.SetInferredArgumentTypes(new[] {leftDelegate.ReturnType});
				}

				var rightType = RightOperand.Resolve(ctx);

				if (!ReflectionHelper.CanCombineDelegates(leftType, rightType))
					Error(Translations.CompilerMessages.DelegatesNotCombinable, leftType, rightType);
			}

			// resolve as a possibly overloaded operator
			return base.resolve(ctx, mustReturn);
		}

		protected override Type ResolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			if (leftType.IsAnyOf(typeof (int), typeof (long)) && rightType == typeof (int))
				return leftType;

			if (!IsLeft && ReflectionHelper.CanCombineDelegates(leftType, rightType))
				return ReflectionHelper.CombineDelegates(leftType, rightType);

			return null;
		}

		#endregion

		#region Transform

		protected override NodeBase Expand(Context ctx, bool mustReturn)
		{
			var leftType = LeftOperand.Resolve(ctx, mustReturn);

			// create a lambda expression that passes the result of left function to the right one
			if (leftType.IsCallableType())
			{
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

		#endregion

		#region Emit

		protected override void EmitOperator(Context ctx)
		{
			var gen = ctx.CurrentMethod.Generator;

			LeftOperand.Emit(ctx, true);
			RightOperand.Emit(ctx, true);

			gen.EmitShift(IsLeft);
		}

		#endregion

		#region Constant unroll

		protected override dynamic UnrollConstant(dynamic left, dynamic right)
		{
			return IsLeft ? left << right : left >> right;
		}

		#endregion
	}
}
