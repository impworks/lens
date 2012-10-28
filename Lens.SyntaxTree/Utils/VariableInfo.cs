using System;

namespace Lens.SyntaxTree.Utils
{
	/// <summary>
	/// A class representing info about a local variable.
	/// </summary>
	public class VariableInfo
	{
		public string Name { get; set; }
		public Type Type { get; set; }
		public bool IsConstant { get; set; }

		#region Equality members

		protected bool Equals(VariableInfo other)
		{
			return string.Equals(Name, other.Name) && Type == other.Type && IsConstant.Equals(other.IsConstant);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((VariableInfo)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = (Name != null ? Name.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Type != null ? Type.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ IsConstant.GetHashCode();
				return hashCode;
			}
		}

		#endregion
	}
}
