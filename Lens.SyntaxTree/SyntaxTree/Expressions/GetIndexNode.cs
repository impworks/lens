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
		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
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

			var idxType = Index.GetExpressionType(ctx);
			var getter = exprType.GetMethod("get_Index", new[] {idxType});
			if (getter != null)
				return getter.ReturnType;

			Error("Type '{0}' cannot be indexed.", exprType);
			return null;
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			if (Expression != null)
				yield return Expression;
			yield return Index;
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
