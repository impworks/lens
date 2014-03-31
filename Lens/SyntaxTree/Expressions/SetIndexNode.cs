using System.Collections.Generic;
using Lens.Compiler;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing assignment to an index.
	/// </summary>
	internal class SetIndexNode : IndexNodeBase
	{
		/// <summary>
		/// Value to be assigned.
		/// </summary>
		public NodeBase Value { get; set; }

		public override IEnumerable<NodeChild> GetChildren()
		{
			yield return new NodeChild(Expression, x => Expression = x);
			yield return new NodeChild(Index, x => Index = x);
			yield return new NodeChild(Value, x => Value = x);
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var exprType = Expression.Resolve(ctx);

			if (exprType.IsArray)
				compileArray(ctx);
			else
				compileCustom(ctx);
		}

		private void compileArray(Context ctx)
		{
			var gen = ctx.CurrentMethod.Generator;

			var exprType = Expression.Resolve(ctx);
			var itemType = exprType.GetElementType();

			Expression.Emit(ctx, true);
			Index.Emit(ctx, true);
			Value.Emit(ctx, true);
			gen.EmitSaveIndex(itemType);
		}

		private void compileCustom(Context ctx)
		{
			var gen = ctx.CurrentMethod.Generator;

			var exprType = Expression.Resolve(ctx);
			var idxType = Index.Resolve(ctx);

			try
			{
				var pty = ctx.ResolveIndexer(exprType, idxType, false);
				var idxDest = pty.ArgumentTypes[0];
				var valDest = pty.ArgumentTypes[1];

				Expression.Emit(ctx, true);

				Expr.Cast(Index, idxDest).Emit(ctx, true);
				Expr.Cast(Value, valDest).Emit(ctx, true);

				gen.EmitCall(pty.MethodInfo);
			}
			catch (LensCompilerException ex)
			{
				ex.BindToLocation(this);
				throw;
			}
		}

		#region Equality members

		protected bool Equals(SetIndexNode other)
		{
			return base.Equals(other) && Equals(Value, other.Value);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((SetIndexNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (base.GetHashCode() * 397) ^ (Value != null ? Value.GetHashCode() : 0);
			}
		}

		#endregion

		public override string ToString()
		{
			return string.Format("setidx({0} of {1} = {2})", Index, Expression, Value);
		}
	}
}
