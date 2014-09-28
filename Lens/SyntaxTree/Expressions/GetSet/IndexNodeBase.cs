namespace Lens.SyntaxTree.Expressions.GetSet
{
	/// <summary>
	/// The base node for accessing array-like structures by index.
	/// </summary>
	internal abstract class IndexNodeBase : AccessorNodeBase
	{
		#region Fields

		/// <summary>
		/// Index expression.
		/// </summary>
		public NodeBase Index { get; set; }

		#endregion

		#region Debug

		protected bool Equals(IndexNodeBase other)
		{
			return Equals(Expression, other.Expression) && Equals(Index, other.Index);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((IndexNodeBase)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Expression != null ? Expression.GetHashCode() : 0) * 397) ^ (Index != null ? Index.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
