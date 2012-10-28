using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node representing the record definition construct.
	/// </summary>
	public class RecordDefinitionNode : NodeBase
	{
		public RecordDefinitionNode()
		{
			Fields = new List<RecordEntry>();
		}

		/// <summary>
		/// Record name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// The fields in the record.
		/// </summary>
		public List<RecordEntry> Fields { get; set; }

		public override Type GetExpressionType()
		{
			return typeof (Unit);
		}

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
