namespace Lens.Compiler
{
	/// <summary>
	/// A pseudotype to represent the absense of value (void).
	/// </summary>
	internal class UnitType
	{
		public override string ToString()
		{
			return "()";
		}

		#region Equality members

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

	/// <summary>
	/// A pseudotype to represent the null variable.
	/// </summary>
	public class NullType { }

	/// <summary>
	/// A pseudotype for expressions which type is to be resolved later: partial application placeholders and lambda arguments
	/// </summary>
	internal class UnspecifiedType { }
}
