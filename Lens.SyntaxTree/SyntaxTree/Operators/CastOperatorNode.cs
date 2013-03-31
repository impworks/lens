using System;
using System.Linq;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing a cast expression.
	/// </summary>
	public class CastOperatorNode : TypeCheckOperatorNodeBase
	{
		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return Type ?? ctx.ResolveType(TypeSignature);
		}

		public override void Compile(Context ctx, bool mustReturn)
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
					var tmpVar = ctx.CurrentScope.DeclareImplicitName(ctx, toType, true);
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
					Error("Cannot cast a null to a value type!");
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
				Error("Delegate types '{0}' and '{1}' do not have matching argument types!", from, to);

			if(!toMethod.ReturnType.IsExtendablyAssignableFrom(fromMethod.ReturnType, true))
				Error("Delegate types '{0}' and '{1}' do not have matching return types!", from, to);

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
			Error("Cannot cast object of type '{0}' to type '{1}'.", from, to);
		}

		public static bool IsImplicitlyBoolean(Type type)
		{
			return type == typeof(bool) || type.GetMethods().Any(m => m.Name == "op_Implicit" && m.ReturnType == typeof (bool));
		}
	}
}
