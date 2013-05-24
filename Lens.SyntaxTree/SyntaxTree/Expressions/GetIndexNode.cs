﻿using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a read-access to an array or list's value.
	/// </summary>
	public class GetIndexNode : IndexNodeBase, IEndLocationTrackingEntity, IPointerProvider
	{
		/// <summary>
		/// Cached property information.
		/// </summary>
		private MethodWrapper m_Getter;

		/// <summary>
		/// Array indexer can return a pointer.
		/// </summary>
		public bool PointerRequired { get; set; }

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var exprType = Expression.GetExpressionType(ctx);
			if (exprType.IsArray)
				return exprType.GetElementType();

			var idxType = Index.GetExpressionType(ctx);
			m_Getter = ctx.ResolveIndexer(exprType, idxType, true);
			return m_Getter.ReturnType;
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
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

			if(PointerRequired)
				gen.EmitLoadIndex(itemType, true);
			else
				gen.EmitLoadIndex(itemType);
		}

		private void compileCustom(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			if (Expression is IPointerProvider)
				(Expression as IPointerProvider).PointerRequired = PointerRequired;
			Expression.Compile(ctx, true);

			var cast = Expr.Cast(Index, m_Getter.ArgumentTypes[0]);
			cast.Compile(ctx, true);

			gen.EmitCall(m_Getter.MethodInfo);
		}

		public override string ToString()
		{
			return string.Format("getidx({0} of {1})", Index, Expression);
		}


		#region Equality

		protected bool Equals(GetIndexNode other)
		{
			return base.Equals(other) && PointerRequired.Equals(other.PointerRequired);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((GetIndexNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (base.GetHashCode() * 397) ^ PointerRequired.GetHashCode();
			}
		}


		#endregion
	}
}
