using System;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A function that has a name.
	/// </summary>
	public class NamedFunctionNode : FunctionNode
	{
		/// <summary>
		/// Function name.
		/// </summary>
		public string Name { get; set; }

		public override void Compile()
		{
			throw new NotImplementedException();

			base.Compile();
		}
	}
}
