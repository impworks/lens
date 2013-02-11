using System;
using System.Collections.Generic;
using System.Reflection;
using Lens.SyntaxTree.Compiler;
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
		/// The type to cast to.
		/// </summary>
		public TypeSignature Type { get; set; }

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Expression;
		}
		
		protected override Type resolveExpressionType(Context ctx)
		{
			return ctx.ResolveType(Type);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var fromType = Expression.GetExpressionType(ctx);
			var toType = ctx.ResolveType(Type);

			if(!toType.IsExtendablyAssignableFrom(fromType) && !fromType.IsExtendablyAssignableFrom(toType))
				Error("Cannot cast object of type '{0}' to type '{1}'!");

			Expression.Compile(ctx, true);

			if (fromType == toType)
				return;

			if (fromType.IsValueType && toType == typeof(object))
				gen.EmitBox(fromType);

			else if (fromType == typeof(object) && toType.IsValueType)
				gen.EmitUnbox(toType);

			else if (fromType.IsNumericType() && toType.IsNumericType())
			{
				// convert to decimal using a constructor that accepts the biggest suiting type
				if (toType == typeof (decimal))
				{
					if (fromType.IsFloatType())
					{
						gen.EmitConvert(typeof (double));
						var ctor = typeof (decimal).GetConstructor(new[] {typeof (double)});
						gen.EmitCreateObject(ctor);
					}
					else
					{
						gen.EmitConvert(typeof(long));
						var ctor = typeof(decimal).GetConstructor(new[] { typeof(long) });
						gen.EmitCreateObject(ctor);
					}
				}

				// convert from decimal to int using op_Explicit
				else if (toType == typeof (decimal))
				{
					// todo!
					throw new NotImplementedException();
				}
				else
				{
					gen.EmitConvert(toType);
				}
			}

			else
				gen.EmitCast(toType, false);
		}

		#region Equality members

		protected bool Equals(CastOperatorNode other)
		{
			return Equals(Expression, other.Expression) && Equals(Type, other.Type);
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
				return ((Expression != null ? Expression.GetHashCode() : 0) * 397) ^ (Type != null ? Type.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
