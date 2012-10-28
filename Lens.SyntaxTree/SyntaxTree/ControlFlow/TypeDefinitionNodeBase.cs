using System;
using System.Collections.Generic;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A base node for algebraic types and records.
	/// </summary>
	public abstract class TypeDefinitionNodeBase<T> : NodeBase
	{
		protected TypeDefinitionNodeBase()
		{
			Entries = new List<T>();
		}

		/// <summary>
		/// The name of the type.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The entries of the type node.
		/// </summary>
		public List<T> Entries { get; private set; }
	}
}
