using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node representing the algebraic type definition construct.
	/// </summary>
	public class TypeDefinitionNode : TypeDefinitionNodeBase<TypeEntry>
	{
		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Gets the list of distinctive entry tag types.
		/// </summary>
		/// <returns></returns>
		private IEnumerable<Type> getDistinctTagTypes()
		{
			var types = new Dictionary<Type, bool>();
			foreach (var curr in Entries)
				types[curr.TagType.Type] = true;

			return types.Keys;
		}
	}

	/// <summary>
	/// Definition of an algebraic type entry.
	/// </summary>
	public class TypeEntry : LocationEntity
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

		protected bool Equals(TypeEntry other)
		{
			return string.Equals(Name, other.Name) && Equals(TagType, other.TagType);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((TypeEntry)obj);
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
