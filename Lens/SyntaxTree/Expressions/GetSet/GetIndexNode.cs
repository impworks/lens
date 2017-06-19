using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.GetSet
{
	/// <summary>
	/// A node representing a read-access to an array or list's value.
	/// </summary>
	internal class GetIndexNode : IndexNodeBase, IPointerProvider
	{
		#region Fields

		/// <summary>
		/// Cached property information.
		/// </summary>
		private MethodWrapper _getter;

		public bool PointerRequired { get; set; }
		public bool RefArgumentRequired { get; set; }

		#endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var exprType = Expression.Resolve(ctx);
			if (exprType.IsArray)
				return exprType.GetElementType();

			var idxType = Index.Resolve(ctx);
			try
			{
				_getter = ReflectionHelper.ResolveIndexer(exprType, idxType, true);
				return _getter.ReturnType;
			}
			catch (LensCompilerException ex)
			{
				ex.BindToLocation(this);
				throw;
			}
		}

		#endregion

		#region Transform

		protected override IEnumerable<NodeChild> GetChildren()
		{
			yield return new NodeChild(Expression, x => Expression = x);
			yield return new NodeChild(Index, x => Index = x);
		}

		#endregion

		#region Emit

		protected override void EmitCode(Context ctx, bool mustReturn)
		{
			if (_getter == null)
				CompileArray(ctx);
			else
				CompileCustom(ctx);
		}

		/// <summary>
		/// Emits the code for retrieving an array item by index.
		/// </summary>
		private void CompileArray(Context ctx)
		{
			var gen = ctx.CurrentMethod.Generator;

			var exprType = Expression.Resolve(ctx);
			var itemType = exprType.GetElementType();

			Expression.Emit(ctx, true);
			Expr.Cast(Index, typeof(int)).Emit(ctx, true);

			gen.EmitLoadIndex(itemType, RefArgumentRequired || PointerRequired);
		}

		/// <summary>
		/// Emits the code for retrieving a value from an object by custom indexer.
		/// </summary>
		private void CompileCustom(Context ctx)
		{
			var retType = _getter.ReturnType;

			if(RefArgumentRequired && retType.IsValueType)
				Error(CompilerMessages.IndexerValuetypeRef, Expression.Resolve(ctx), retType);

			var gen = ctx.CurrentMethod.Generator;

			var ptrExpr = Expression as IPointerProvider;
			if (ptrExpr != null)
			{
				ptrExpr.PointerRequired = PointerRequired;
				ptrExpr.RefArgumentRequired = RefArgumentRequired;
			}

			Expression.Emit(ctx, true);

			Expr.Cast(Index, _getter.ArgumentTypes[0]).Emit(ctx, true);

			gen.EmitCall(_getter.MethodInfo, _getter.IsVirtual);
		}

		#endregion

		#region Debug

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

		public override string ToString()
		{
			return string.Format("getidx({0} of {1})", Index, Expression);
		}

		#endregion
	}
}
