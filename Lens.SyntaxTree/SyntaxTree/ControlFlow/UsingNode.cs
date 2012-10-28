using System;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	public class UsingNode : NodeBase
	{
		/// <summary>
		/// Namespace to be resolved.
		/// </summary>
		public string Namespace { get; set; }

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
