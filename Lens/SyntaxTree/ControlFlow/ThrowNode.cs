using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node representing the exception being thrown or rethrown.
	/// </summary>
	internal class ThrowNode : NodeBase
	{
		/// <summary>
		/// The exception expression to be thrown.
		/// </summary>
		public NodeBase Expression { get; set; }

		public override IEnumerable<NodeChild> GetChildren()
		{
			yield return new NodeChild(Expression, x => Expression = x);
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			if (Expression == null)
			{
				if(ctx.CurrentCatchBlock == null)
					error(CompilerMessages.ThrowArgumentExpected);

				gen.EmitRethrow();
			}
			else
			{
				var type = Expression.Resolve(ctx);

				if (!ctx.ReflectionResolver.IsExtendablyAssignableFrom(typeof (Exception), type))
					error(Expression, CompilerMessages.ThrowTypeNotException, type);

				Expression.Emit(ctx, true);
				gen.EmitThrow();
			}
		}

		#region Equality members

		protected bool Equals(ThrowNode other)
		{
			return Equals(Expression, other.Expression);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((ThrowNode)obj);
		}

		public override int GetHashCode()
		{
			return (Expression != null ? Expression.GetHashCode() : 0);
		}

		#endregion

		public override string ToString()
		{
			return string.Format("throw({0})", Expression);
		}
	}
}
