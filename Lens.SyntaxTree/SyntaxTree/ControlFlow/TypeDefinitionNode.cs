using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node representing the algebraic type definition construct.
	/// </summary>
	public class TypeDefinitionNode : NodeBase
	{
		public TypeDefinitionNode()
		{
			Entries = new List<TypeEntry>();
		}

		/// <summary>
		/// The entries found within a type.
		/// </summary>
		public List<TypeEntry> Entries { get; set; }

		public override Type GetExpressionType()
		{
			return typeof (Unit);
		}

		public override void Compile()
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
	public class TypeEntry
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
	}
}
