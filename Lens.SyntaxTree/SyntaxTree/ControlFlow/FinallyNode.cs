using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The block of code that executes when a 'try' block is being left.
	/// </summary>
	public class FinallyNode : NodeBase
	{
		public FinallyNode()
		{
			Code = new CodeBlockNode();	
		}

		public CodeBlockNode Code { get; set; }

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Code;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}
	}
}
