using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that raises one value to the power of another value.
	/// </summary>
	public class PowOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "**"; }
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return typeof (double);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			var method = typeof (Math).GetMethod("Pow", new[] {typeof (double), typeof (double)});

			loadAndConvertNumerics(ctx, typeof(double));
			gen.EmitCall(method);
		}
	}
}
