using System;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node representing the exception being thrown or rethrown.
	/// </summary>
	public class ThrowNode : NodeBase
	{
		/// <summary>
		/// The exception expression to be thrown.
		/// </summary>
		public NodeBase Expression { get; set; }

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
