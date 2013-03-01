using System;
using System.Collections.Generic;
using System.Reflection;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a read-access to an array or list's value.
	/// </summary>
	public class GetIndexNode : IndexNodeBase, IEndLocationTrackingEntity
	{
		private PropertyInfo IndexProperty;

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var exprType = Expression.GetExpressionType(ctx);
			if (exprType.IsArray)
				return exprType.GetElementType();

			var idxType = Index.GetExpressionType(ctx);
			IndexProperty = findIndexer(exprType, idxType, false);
			return IndexProperty.GetGetMethod().ReturnType;
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			if (Expression != null)
				yield return Expression;

			yield return Index;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			// ensure validation
			GetExpressionType(ctx);

			var exprType = Expression.GetExpressionType(ctx);

			if (exprType.IsArray)
				compileArray(ctx);
			else
				compileCustom(ctx);
		}

		private void compileArray(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			var exprType = Expression.GetExpressionType(ctx);
			var itemType = exprType.GetElementType();

			Expression.Compile(ctx, true);
			Index.Compile(ctx, true);
			gen.EmitLoadIndex(itemType);
		}

		private void compileCustom(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;
			var method = IndexProperty.GetGetMethod();

			Expression.Compile(ctx, true);

			var cast = Expr.Cast(Index, method.GetParameters()[0].ParameterType);
			cast.Compile(ctx, true);

			gen.EmitCall(method);
		}

		public override string ToString()
		{
			return string.Format("getidx({0} of {1})", Index, Expression);
		}
	}
}
