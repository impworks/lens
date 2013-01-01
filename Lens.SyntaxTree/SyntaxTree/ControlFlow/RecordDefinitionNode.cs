﻿using System;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node representing the record definition construct.
	/// </summary>
	public class RecordDefinitionNode : TypeDefinitionNodeBase<RecordEntry>
	{
		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Definition of a record entry.
	/// </summary>
	public class RecordEntry : LocationEntity, IStartLocationTrackingEntity
	{
		/// <summary>
		/// The name of the entry.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The type of the entry.
		/// </summary>
		public TypeSignature Type { get; set; }

		#region Equality members

		protected bool Equals(RecordEntry other)
		{
			return string.Equals(Name, other.Name) && Equals(Type, other.Type);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((RecordEntry)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Type != null ? Type.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
