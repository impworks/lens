using System;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;

namespace Lens.SyntaxTree.Operators.TypeBased
{
	/// <summary>
	/// A node representing a cast expression.
	/// </summary>
	internal class CastOperatorNode : TypeCheckOperatorNodeBase
	{
		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var type = Type ?? ctx.ResolveType(TypeSignature);
			EnsureLambdaInferred(ctx, Expression, type);
			return type;
		}

		#endregion

		#region Transform

		protected override NodeBase Expand(Context ctx, bool mustReturn)
		{
			var fromType = Expression.Resolve(ctx);
			var toType = Resolve(ctx);

			if (fromType.IsNullableType() && !toType.IsNullableType())
				return Expr.Cast(Expr.GetMember(Expression, "Value"), toType);

			return base.Expand(ctx, mustReturn);
		}

		#endregion

		#region Emit

		protected override void EmitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			var fromType = Expression.Resolve(ctx);
			var toType = Resolve(ctx);

			if (toType.IsExtendablyAssignableFrom(fromType, true))
				Expression.Emit(ctx, true);

			else if (fromType.IsNumericType() && toType.IsNumericType(true)) // (decimal -> T) is processed via op_Explicit()
				CastNumeric(ctx, fromType, toType);

			else if(fromType.IsCallableType() && toType.IsCallableType())
				CastDelegate(ctx, fromType, toType);

			else if (fromType == typeof (NullType))
			{
				if (toType.IsNullableType())
				{
					var tmpVar = ctx.Scope.DeclareImplicit(ctx, toType, true);
					gen.EmitLoadLocal(tmpVar.LocalBuilder, true);
					gen.EmitInitObject(toType);
					gen.EmitLoadLocal(tmpVar.LocalBuilder);
				}

				else if (!toType.IsValueType)
					gen.EmitNull();

				else
					Error(CompilerMessages.CastNullValueType, toType);
			}

			else if (toType.IsNullableType())
			{
				Expression.Emit(ctx, true);

				var underlying = Nullable.GetUnderlyingType(toType);
				if(underlying.IsNumericType() && fromType.IsNumericType() && underlying != fromType)
					gen.EmitConvert(underlying);
				else if(underlying != fromType)
					Error(fromType, toType);

				var ctor = toType.GetConstructor(new[] { underlying });
				gen.EmitCreateObject(ctor);
			}

			else if (toType.IsExtendablyAssignableFrom(fromType))
			{
				Expression.Emit(ctx, true);

				// box
				if (fromType.IsValueType && toType == typeof (object))
					gen.EmitBox(fromType);

				else
				{
					var castOp = ctx.ResolveConvertorToType(fromType, toType);
					if (castOp != null)
						gen.EmitCall(castOp.MethodInfo);
					else
						gen.EmitCast(toType);
				}
			}

			else if (fromType.IsExtendablyAssignableFrom(toType))
			{
				Expression.Emit(ctx, true);

				// unbox
				if (fromType == typeof (object) && toType.IsValueType)
					gen.EmitUnbox(toType);

				// cast ancestor to descendant
				else if (!fromType.IsValueType && !toType.IsValueType)
					gen.EmitCast(toType);

				else
				{
					var castOp = ctx.ResolveConvertorToType(fromType, toType);
					if (castOp != null)
						gen.EmitCall(castOp.MethodInfo);
					else
						Error(fromType, toType);
				}
			}

			else
				Error(fromType, toType);
		}

		private void CastDelegate(Context ctx, Type from, Type to)
		{
			var gen = ctx.CurrentMethod.Generator;

			var toCtor = ctx.ResolveConstructor(to, new[] {typeof (object), typeof (IntPtr)});
			var fromMethod = ctx.ResolveMethod(from, "Invoke");
			var toMethod = ctx.ResolveMethod(to, "Invoke");

			var fromArgs = fromMethod.ArgumentTypes;
			var toArgs = toMethod.ArgumentTypes;

			if(fromArgs.Length != toArgs.Length || toArgs.Select((ta, id) => !ta.IsExtendablyAssignableFrom(fromArgs[id], true)).Any(x => x))
				Error(CompilerMessages.CastDelegateArgTypesMismatch, from, to);

			if(!toMethod.ReturnType.IsExtendablyAssignableFrom(fromMethod.ReturnType, true))
				Error(CompilerMessages.CastDelegateReturnTypesMismatch, to, from, toMethod.ReturnType, fromMethod.ReturnType);

			if (fromMethod.IsStatic)
				gen.EmitNull();
			else
				Expression.Emit(ctx, true);

			if (from.IsGenericType && to.IsGenericType && from.GetGenericTypeDefinition() == to.GetGenericTypeDefinition())
				return;

			gen.EmitLoadFunctionPointer(fromMethod.MethodInfo);
			gen.EmitCreateObject(toCtor.ConstructorInfo);
		}

		private void CastNumeric(Context ctx, Type from, Type to)
		{
			var gen = ctx.CurrentMethod.Generator;
			
			Expression.Emit(ctx, true);

			if (to == typeof (decimal))
			{
				var ctor = ctx.ResolveConstructor(typeof (decimal), new[] { from });
				if (ctor == null)
				{
					ctor = ctx.ResolveConstructor(typeof(decimal), new[] { typeof(int) });
					gen.EmitConvert(typeof(int));
				}

				gen.EmitCreateObject(ctor.ConstructorInfo);
			}
			else
			{
				gen.EmitConvert(to);
			}
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Displays a default error for uncastable types.
		/// </summary>
		private void Error(Type from, Type to)
		{
			Error(CompilerMessages.CastTypesMismatch, from, to);
		}

		#endregion
	}
}
