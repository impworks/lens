using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

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
			var indexPty = findGetter(exprType, idxType);
			if (indexPty != null)
				return indexPty.GetGetMethod().ReturnType;

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

			var exprType = Expression.GetExpressionType(ctx);
			var idxType = Index.GetExpressionType(ctx);

			var indexPty = findGetter(exprType, idxType);
			var method = indexPty.GetGetMethod();

			Expression.Compile(ctx, true);

			var cast = Expr.Cast(Index, method.GetParameters()[0].ParameterType);
			cast.Compile(ctx, true);

			gen.EmitCall(method);
		}

		private PropertyInfo findGetter(Type exprType, Type idxType)
		{
			return exprType.GetProperties().FirstOrDefault(
				p =>
				{
					var args = p.GetIndexParameters();
					return args.Length == 1 && args[0].ParameterType.IsExtendablyAssignableFrom(idxType);
				}
			);
		}

		public override string ToString()
		{
			return string.Format("getidx({0} of {1})", Index, Expression);
		}
	}
}
