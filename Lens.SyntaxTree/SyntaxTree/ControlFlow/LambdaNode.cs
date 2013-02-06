using System;
using System.Linq;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node that represents the lambda function.
	/// </summary>
	public class LambdaNode : FunctionNodeBase
	{
		public override void ProcessClosures(Context ctx)
		{
			var argTypes = Arguments.Values.Select(a => ctx.ResolveType(a.Type.Signature)).ToArray();
			var methodBackup = ctx.CurrentMethod;
			ctx.CurrentMethod = ctx.CurrentScope.CreateClosureMethod(ctx, argTypes);

			base.ProcessClosures(ctx);

			ctx.CurrentMethod = methodBackup;
		}

		protected override Type resolveExpressionType(Context ctx)
		{
			// todo!
			return base.resolveExpressionType(ctx);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		#region Helpers

		#endregion
	}
}
