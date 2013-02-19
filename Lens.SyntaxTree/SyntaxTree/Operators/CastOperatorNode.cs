using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing a cast expression.
	/// </summary>
	public class CastOperatorNode : NodeBase
	{
		/// <summary>
		/// The expression to cast.
		/// </summary>
		public NodeBase Expression { get; set; }

		/// <summary>
		/// The type signature to cast to.
		/// </summary>
		public TypeSignature TypeSignature { get; set; }

		/// <summary>
		/// A resolved type to cast to.
		/// </summary>
		public Type Type { get; set; }

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Expression;
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return Type ?? ctx.ResolveType(TypeSignature);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var fromType = Expression.GetExpressionType(ctx);
			var toType = Type ?? ctx.ResolveType(TypeSignature);

			if (fromType == toType)
				Expression.Compile(ctx, true);

			else if (fromType.IsNumericType() && toType.IsNumericType())
			{
				Expression.Compile(ctx, true);
				gen.EmitConvert(toType);
			}

			else if (fromType == typeof (NullType))
			{
				if (toType.IsNullableType())
				{
					var tmpVar = ctx.CurrentScope.DeclareImplicitName(ctx, toType, true);
					gen.EmitLoadLocalAddress(tmpVar);
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
					var explicitOp = fromType.GetMethods().FirstOrDefault(m => m.Name == "op_Explicit" && m.ReturnType == toType);
					if (explicitOp != null)
						gen.EmitCall(explicitOp);
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
					error(fromType, toType);
			}

			else
				error(fromType, toType);
		}

		private void error(Type from, Type to)
		{
			Error("Cannot cast object of type '{0}' to type '{1}'.", from, to);
		}

		public static bool IsImplicitlyBoolean(Type type)
		{
			return type == typeof(bool) || type.GetMethods().Any(m => m.Name == "op_Implicit" && m.ReturnType == typeof (bool));
		}

		public static void CompileAsBoolean(NodeBase node, Context ctx)
		{
			var gen = ctx.CurrentILGenerator;
			var type = node.GetExpressionType(ctx);

			node.Compile(ctx, true);

			if (type != typeof (bool))
			{
				var implConv = type.GetMethods().FirstOrDefault(m => m.Name == "op_Implicit" && m.ReturnType == typeof (bool));
				if (implConv == null)
					node.Error("Type '{0}' cannot be used in boolean context!", type);

				gen.EmitCall(implConv);
			}
		}

		#region Equality members

		protected bool Equals(CastOperatorNode other)
		{
			return Equals(Expression, other.Expression) && Equals(TypeSignature, other.TypeSignature);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((CastOperatorNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Expression != null ? Expression.GetHashCode() : 0) * 397) ^ (TypeSignature != null ? TypeSignature.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
