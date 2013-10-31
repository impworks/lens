using System;
using System.Linq;
using Lens.Compiler;
using Lens.SyntaxTree.Literals;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing a cast expression.
	/// </summary>
	internal class CastOperatorNode : TypeCheckOperatorNodeBase
	{
		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return Type ?? ctx.ResolveType(TypeSignature);
		}

		protected override void compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var fromType = Expression.GetExpressionType(ctx);
			var toType = GetExpressionType(ctx);

			if (toType.IsExtendablyAssignableFrom(fromType, true))
				Expression.Compile(ctx, true);

			else if (fromType.IsNumericType() && toType.IsNumericType())
			{
				Expression.Compile(ctx, true);
				gen.EmitConvert(toType);
			}

			else if(fromType.IsCallableType() && toType.IsCallableType())
				castDelegate(ctx, fromType, toType);

			else if (fromType == typeof (NullType))
			{
				if (toType.IsNullableType())
				{
					var tmpVar = ctx.CurrentScope.DeclareImplicitName(toType, true);
					gen.EmitLoadLocal(tmpVar, true);
					gen.EmitInitObject(toType);
					gen.EmitLoadLocal(tmpVar);
				}

				else if (!toType.IsValueType)
				{
					Expression.Compile(ctx, true);
					gen.EmitCast(toType);
				}

				else
					Error(CompilerMessages.CastNullValueType, toType);
			}

			else if (toType.IsExtendablyAssignableFrom(fromType))
			{
				Expression.Compile(ctx, true);

				// box
				if (fromType.IsValueType && toType == typeof (object))
					gen.EmitBox(fromType);

				// nullable
				else if (toType.IsNullableType() && Nullable.GetUnderlyingType(toType) == fromType)
				{
					var ctor = toType.GetConstructor(new[] {fromType});
					gen.EmitCreateObject(ctor);
				}

				else
				{
					// todo: a more elegant approach maybe?
					var castOp = fromType.GetMethods().Where(m => m.Name == "op_Explicit" || m.Name == "op_Implicit" && m.ReturnType == toType)
													  .OrderBy(m => m.Name == "op_Implicit" ? 0 : 1)
													  .FirstOrDefault();
					if (castOp != null)
						gen.EmitCall(castOp);
					else
						gen.EmitCast(toType);
				}
			}

			else if (fromType.IsExtendablyAssignableFrom(toType))
			{
				Expression.Compile(ctx, true);

				// unbox
				if (fromType == typeof (object) && toType.IsValueType)
					gen.EmitUnbox(toType);

				// cast ancestor to descendant
				else if (!fromType.IsValueType && !toType.IsValueType)
					gen.EmitCast(toType);

				else
					castError(fromType, toType);
			}

			else
				castError(fromType, toType);
		}

		private void castDelegate(Context ctx, Type from, Type to)
		{
			var gen = ctx.CurrentILGenerator;

			var toCtor = ctx.ResolveConstructor(to, new[] {typeof (object), typeof (IntPtr)});
			var fromMethod = ctx.ResolveMethod(from, "Invoke");
			var toMethod = ctx.ResolveMethod(to, "Invoke");

			var fromArgs = fromMethod.ArgumentTypes;
			var toArgs = toMethod.ArgumentTypes;

			if(fromArgs.Length != toArgs.Length || toArgs.Select((ta, id) => !ta.IsExtendablyAssignableFrom(fromArgs[id], true)).Any(x => x))
				Error(CompilerMessages.CastDelegateArgTypesMismatch, from, to);

			if(!toMethod.ReturnType.IsExtendablyAssignableFrom(fromMethod.ReturnType, true))
				Error(CompilerMessages.CastDelegateReturnTypesMismatch, from, to);

			if (fromMethod.IsStatic)
				gen.EmitNull();
			else
				Expression.Compile(ctx, true);

			if (from.IsGenericType && to.IsGenericType && from.GetGenericTypeDefinition() == to.GetGenericTypeDefinition())
				return;

			gen.EmitLoadFunctionPointer(fromMethod.MethodInfo);
			gen.EmitCreateObject(toCtor.ConstructorInfo);
		}

		private void castError(Type from, Type to)
		{
			Error(CompilerMessages.CastTypesMismatch, from, to);
		}

		public static bool IsImplicitlyBoolean(Type type)
		{
			return type == typeof(bool) || type.GetMethods().Any(m => m.Name == "op_Implicit" && m.ReturnType == typeof (bool));
		}
	}
}
