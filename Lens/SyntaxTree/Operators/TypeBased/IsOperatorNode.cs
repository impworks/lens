using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Operators.TypeBased
{
	/// <summary>
	/// Checks if the object is of given type.
	/// </summary>
	internal class IsOperatorNode : TypeCheckOperatorNodeBase
	{
		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn = true)
		{
			return typeof (bool);
		}

		#endregion

		#region Emit

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			var exprType = Expression.Resolve(ctx);
			var desiredType = Type ?? ctx.ResolveType(TypeSignature);

			checkTypeInSafeMode(ctx, desiredType);

			// types are identical
			if (exprType == desiredType)
			{
				gen.EmitConstant(true);
				return;
			}

			// valuetype can only be cast to object
			if (exprType.IsValueType)
			{
				gen.EmitConstant(desiredType == typeof(object));
				return;
			}

			Expression.Emit(ctx, true);

			// check if not null
			if (desiredType == typeof (object))
			{
				gen.EmitNull();
				gen.EmitCompareEqual();
				gen.EmitConstant(false);
				gen.EmitCompareEqual();
			}
			else
			{
				gen.EmitCast(desiredType, false);
				gen.EmitNull();
				gen.EmitCompareGreater(false);
			}
		}

		#endregion
	}
}
