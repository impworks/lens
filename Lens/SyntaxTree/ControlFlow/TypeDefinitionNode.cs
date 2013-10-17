using Lens.Compiler;

namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node representing the algebraic type definition construct.
	/// </summary>
	public class TypeDefinitionNode : TypeDefinitionNodeBase<TypeLabel>
	{ }

	/// <summary>
	/// Definition of an algebraic type entry.
	/// </summary>
	public class TypeLabel : LocationEntity
	{
		/// <summary>
		/// The name of the entry.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The tag associated with the entry.
		/// </summary>
		public TypeSignature TagType { get; set; }

		/// <summary>
		/// Checks whether the entry has a tag.
		/// </summary>
		public bool IsTagged { get { return TagType != null; } }

		#region Equality members

		protected bool Equals(TypeLabel other)
		{
			return string.Equals(Name, other.Name) && Equals(TagType, other.TagType);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((TypeLabel)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (TagType != null ? TagType.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
