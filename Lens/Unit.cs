namespace Lens
{
	/// <summary>
	/// The void-like class for LENS.
	/// </summary>
	public class Unit
	{
		public override string ToString()
		{
			return "()";
		}

		#region Equality members

		protected bool Equals(Unit other)
		{
			return true;
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType();
		}

		public override int GetHashCode()
		{
			return 0;
		}

		#endregion
	}
}
