using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a read-access to an array or list's value.
	/// </summary>
	internal class GetIndexNode : IndexNodeBase, IPointerProvider
	{
		/// <summary>
		/// Cached property information.
		/// </summary>
		private MethodWrapper m_Getter;

		public bool PointerRequired { get; set; }
		public bool RefArgumentRequired { get; set; }

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var exprType = Expression.Resolve(ctx);
			if (exprType.IsArray)
				return exprType.GetElementType();

			var idxType = Index.Resolve(ctx);
			try
			{
				m_Getter = ctx.ResolveIndexer(exprType, idxType, true);
				return m_Getter.ReturnType;
			}
			catch (LensCompilerException ex)
			{
				ex.BindToLocation(this);
				throw;
			}
		}

		public override IEnumerable<NodeChild> GetChildren()
		{
			yield return new NodeChild(Expression, x => Expression = x);
			yield return new NodeChild(Index, x => Index = x);
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			// ensure validation
			Resolve(ctx);

			var exprType = Expression.Resolve(ctx);

			if (exprType.IsArray)
				compileArray(ctx);
			else
				compileCustom(ctx);
		}

		private void compileArray(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			var exprType = Expression.Resolve(ctx);
			var itemType = exprType.GetElementType();

			Expression.Emit(ctx, true);
			Index.Emit(ctx, true);

			gen.EmitLoadIndex(itemType, RefArgumentRequired || PointerRequired);
		}

		private void compileCustom(Context ctx)
		{
			var retType = m_Getter.ReturnType;
			if(RefArgumentRequired && retType.IsValueType)
				error(CompilerMessages.IndexerValuetypeRef, Expression.Resolve(ctx), retType);

			var gen = ctx.CurrentILGenerator;

			if (Expression is IPointerProvider)
			{
				var expr = Expression as IPointerProvider;
				expr.PointerRequired = PointerRequired;
				expr.RefArgumentRequired = RefArgumentRequired;
			}

			Expression.Emit(ctx, true);

			var cast = Expr.Cast(Index, m_Getter.ArgumentTypes[0]);
			cast.Emit(ctx, true);

			gen.EmitCall(m_Getter.MethodInfo);
		}

		public override string ToString()
		{
			return string.Format("getidx({0} of {1})", Index, Expression);
		}


		#region Equality

		protected bool Equals(GetIndexNode other)
		{
			return base.Equals(other)
				   && RefArgumentRequired.Equals(other.RefArgumentRequired)
				   && PointerRequired.Equals(other.PointerRequired);
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
				var hash = base.GetHashCode();
				hash = (hash * 397) ^ PointerRequired.GetHashCode();
				hash = (hash * 397) ^ RefArgumentRequired.GetHashCode();
				return hash;
			}
		}


		#endregion
	}
}
