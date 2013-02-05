using Lens.SyntaxTree.SyntaxTree.ControlFlow;
using Lens.Utils;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// The base entity for a method and a constructor that allows lookup by argument types.
	/// </summary>
	abstract class MethodEntityBase : TypeContentsBase
	{
		protected MethodEntityBase()
		{
			Body = new CodeBlockNode();
			Arguments = new HashList<FunctionArgument>();
			Scope = new Scope();
		}

		/// <summary>
		/// The argument list.
		/// </summary>
		public readonly HashList<FunctionArgument> Arguments;

		/// <summary>
		/// The body of the method.
		/// </summary>
		public CodeBlockNode Body { get; private set; }

		/// <summary>
		/// The scope of the method.
		/// </summary>
		public Scope Scope { get; private set; }

		/// <summary>
		/// Process closures.
		/// </summary>
		public void ProcessClosures(Context ctx)
		{
			var oldMethod = ctx.CurrentMethod;
			ctx.CurrentMethod = this;
			Body.ProcessClosures(ctx);
			ctx.CurrentMethod = oldMethod;
		}
	}
}
