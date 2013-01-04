using System;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node representing the algebraic type definition construct.
	/// </summary>
	public class TypeDefinitionNode : TypeDefinitionNodeBase<TypeLabel>
	{
		/// <summary>
		/// Prepares the assembly entities for the type.
		/// </summary>
		public void PrepareSelf(Context ctx)
		{
			if (TypeBuilder != null)
				throw new InvalidOperationException(string.Format("Type {0} has already been prepared!", Name));

			TypeBuilder = ctx.MainModule.DefineType(
				Name,
				TypeAttributes.Public | TypeAttributes.Class
			);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Definition of an algebraic type entry.
	/// </summary>
	public class TypeLabel : LocationEntity
	{
		/// <summary>
		/// The type of this label.
		/// </summary>
		public TypeDefinitionNode ContainingType { get; set; }

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

		/// <summary>
		/// The type builder.
		/// </summary>
		public TypeBuilder TypeBuilder { get; private set; }

		/// <summary>
		/// The field builder for the tag.
		/// </summary>
		public FieldBuilder FieldBuilder { get; private set; }

		/// <summary>
		/// Registers the subtype for this label.
		/// </summary>
		public void PrepareSelf(TypeDefinitionNode root, Context ctx)
		{
			if (TypeBuilder != null)
				throw new InvalidOperationException(string.Format("Label {0} has already been prepared!", Name));

			ContainingType = root;
			TypeBuilder = ctx.MainModule.DefineType(
				Name,
				TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed,
				ContainingType.TypeBuilder
			);
		}

		/// <summary>
		/// Registers a field for this label.
		/// </summary>
		public void PrepareTag(Context ctx)
		{
			if (!IsTagged)
				return;

			if (FieldBuilder != null)
				throw new InvalidOperationException(string.Format("Tag for label {0} has already been prepared!", Name));

			FieldBuilder = TypeBuilder.DefineField(
				"Tag",
				ctx.ResolveType(TagType.Signature),
				FieldAttributes.Public
			);
		}

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
