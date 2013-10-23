using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.SyntaxTree.Expressions;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators
{
	internal class ShiftOperatorNode : BinaryOperatorNodeBase
	{
		public bool IsLeft { get; set; }

		/// <summary>
		/// The resulting method if the shift operation is used in function composition semantics.
		/// </summary>
		private MethodEntity _Method;
	
		public override void ProcessClosures(Context ctx)
		{
			var leftType = LeftOperand.GetExpressionType(ctx);

			var rightGetter = RightOperand as GetMemberNode;
			if (!IsLeft && leftType.IsCallableType() && rightGetter != null)
			{
				if (rightGetter.TypeHints.IsEmpty())
				{
					var returnType = ctx.ResolveMethod(leftType, "Invoke").ReturnType;
					rightGetter.TypeHints = new List<TypeSignature> {TypeSignature.Parse(returnType.FullName)};
				}
			}

			var rightType = RightOperand.GetExpressionType(ctx);

			if (rightGetter != null)
				rightGetter.TypeHints.Clear();

			if (!IsLeft && leftType.IsCallableType() && rightType.IsCallableType())
			{
				if (!ctx.CanCombineDelegates(leftType, rightType))
					Error(Translations.CompilerMessages.DelegatesNotCombinable, leftType, rightType);

				var argTypes = ctx.WrapDelegate(leftType).ArgumentTypes;
				var argGetters = argTypes.Select((a, id) => Expr.GetArg(id)).Cast<NodeBase>().ToArray();

				_Method = ctx.CurrentScope.CreateClosureMethod(ctx, argTypes, ctx.WrapDelegate(rightType).ReturnType);
				_Method.Body = 
					Expr.Block(
						Expr.Invoke(
							RightOperand,
							Expr.Invoke(
								LeftOperand,
								argGetters
							)
						)
					);

				var methodBackup = ctx.CurrentMethod;
				ctx.CurrentMethod = _Method;

				var scope = _Method.Scope;
				scope.InitializeScope(ctx);

				_Method.Body.ProcessClosures(ctx);
				_Method.PrepareSelf();

				scope.FinalizeScope(ctx);

				ctx.CurrentMethod = methodBackup;
			}
			else
			{
				base.ProcessClosures(ctx);
			}
		}

		public override string OperatorRepresentation
		{
			get { return IsLeft ? "<:" : ":>"; }
		}

		public override string OverloadedMethodName
		{
			get { return IsLeft ? "op_LeftShift" : "op_RightShift"; }
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
			if(_Method == null)
				compileShift(ctx);
			else
				compileComposition(ctx);
		}

		private void compileShift(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			LeftOperand.Compile(ctx, true);
			RightOperand.Compile(ctx, true);

			if (IsLeft)
				gen.EmitShiftLeft();
			else
				gen.EmitShiftRight();
		}

		private void compileComposition(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			// find constructor
			var type = FunctionalHelper.CreateDelegateType(_Method.ReturnType, _Method.ArgumentTypes);
			var ctor = ctx.ResolveConstructor(type, new[] { typeof(object), typeof(IntPtr) });

			var closureInstance = ctx.CurrentScope.ClosureVariable;
			gen.EmitLoadLocal(closureInstance);
			gen.EmitLoadFunctionPointer(_Method.MethodBuilder);
			gen.EmitCreateObject(ctor.ConstructorInfo);
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return IsLeft ? left << right : left >> right;
		}
	}
}
