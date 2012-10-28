using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node representing the record definition construct.
	/// </summary>
	public class RecordDefinitionNode : TypeDefinitionNodeBase<RecordEntry>
	{
		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Definition of a record entry.
	/// </summary>
	public class RecordEntry
	{
		/// <summary>
		/// The name of the entry.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The type of the entry.
		/// </summary>
		public TypeSignature Type { get; set; }
	}
}
