using Lens.Compiler;

namespace Lens.SyntaxTree.Internals
{
	/// <summary>
	/// A node that does nothing.
	/// Can be used as replacement by other nodes when their Resolve() method finds out they are irrelevant.
	/// </summary>
	internal class NopNode : NodeBase
	{
		protected override void emitCode(Context ctx, bool mustReturn)
		{
			// does nothing and throws no warning
		}
	}
}
