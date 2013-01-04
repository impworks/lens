using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;

namespace Lens.SyntaxTree.SyntaxTree
{
	/// <summary>
	/// The root node representing the whole program.
	/// </summary>
	public class RootNode : NodeBase
	{
		public RootNode()
		{
			UsingNamespaces = new List<string>();
			Types = new List<TypeDefinitionNode>();
			Records = new List<RecordDefinitionNode>();
			Code = new CodeBlockNode();
		}

		/// <summary>
		/// The namespaces resolved with the 'using' keyword.
		/// </summary>
		public List<string> UsingNamespaces { get; private set; }

		/// <summary>
		/// Declared types.
		/// </summary>
		public List<TypeDefinitionNode> Types { get; private set; }

		/// <summary>
		/// Declared records.
		/// </summary>
		public List<RecordDefinitionNode> Records { get; private set; }

		/// <summary>
		/// The code of the program.
		/// </summary>
		public CodeBlockNode Code { get; private set; }

		public override Type GetExpressionType(Context ctx)
		{
			throw new InvalidOperationException("Root node!");
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}
	}
}
