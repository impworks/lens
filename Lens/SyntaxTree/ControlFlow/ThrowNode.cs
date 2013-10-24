using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node representing the exception being thrown or rethrown.
	/// </summary>
	internal class ThrowNode : NodeBase, IStartLocationTrackingEntity
	{
		/// <summary>
		/// The exception expression to be thrown.
		/// </summary>
		public NodeBase Expression { get; set; }

		public override LexemLocation EndLocation
		{
			get { return Expression.EndLocation; }
			set { LocationSetError(); }
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Expression;
		}

		protected override void compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			if (Expression == null)
			{
				if(ctx.CurrentCatchBlock == null)
					Error(CompilerMessages.ThrowArgumentExpected);

				gen.EmitRethrow();
			}
			else
			{
				var type = Expression.GetExpressionType(ctx);

				if (!typeof (Exception).IsExtendablyAssignableFrom(type))
					Error(Expression, CompilerMessages.ThrowTypeNotException);

				Expression.Compile(ctx, true);
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
