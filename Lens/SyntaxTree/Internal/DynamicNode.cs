using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Internal
{
	/// <summary>
	/// A node that executes an arbitrary block of code when compiled.
	/// Is used when a bit of code must be injected into an existing node.
	/// </summary>
	internal class DynamicNode : NodeBase
	{
		public DynamicNode(Action<Context> body)
		{
			_Body = body;
		}

		private readonly Action<Context> _Body;

		protected override void compile(Context ctx, bool mustReturn)
		{
			_Body(ctx);
		}
	}
}
