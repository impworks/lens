using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a read-access to an array or list's value.
	/// </summary>
	public class GetIndexNode : IndexNodeBase, IEndLocationTrackingEntity
	{
		protected override Type resolveExpressionType(Context ctx)
		{
			var exprType = Expression.GetExpressionType(ctx);
			if (exprType.IsArray)
				return exprType.GetElementType();

			if (exprType.IsGenericType)
			{
				var gt = exprType.GetGenericTypeDefinition();
				var args = exprType.GetGenericArguments();
				if (gt == typeof (List<>))
					return args[0];
				if (gt == typeof (Dictionary<,>))
					return args[1];
			}
			else
			{
				Error("Type '{0}' cannot be indexed.", exprType);
			}

			return base.resolveExpressionType(ctx);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			return string.Format("getidx({0} of {1})", Index, Expression);
		}
	}
}
