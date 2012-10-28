using System;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The safe block of code.
	/// </summary>
	public class CatchNode : NodeBase
	{
		/// <summary>
		/// The code block.
		/// </summary>
		public CodeBlockNode Code { get; set; }

		public override Utils.LexemLocation EndLocation
		{
			get { return Code.EndLocation; }
			set { LocationSetError(); }
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
