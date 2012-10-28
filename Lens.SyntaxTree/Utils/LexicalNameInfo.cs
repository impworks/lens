using System;

namespace Lens.SyntaxTree.Utils
{
	/// <summary>
	/// A class representing info about a local variable.
	/// </summary>
	public class LexicalNameInfo
	{
		/// <summary>
		/// Variable name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Variable type.
		/// </summary>
		public Type Type { get; set; }

		/// <summary>
		/// Is the name a constant or a variable?
		/// </summary>
		public bool IsConstant { get; set; }

		#region Equality members

		protected bool Equals(LexicalNameInfo other)
		{
			return string.Equals(Name, other.Name) && Type == other.Type && IsConstant.Equals(other.IsConstant);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((LexicalNameInfo)obj);
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
