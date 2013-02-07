using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node representing the exception being thrown or rethrown.
	/// </summary>
	public class ThrowNode : NodeBase, IStartLocationTrackingEntity
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

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
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
