using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that adds two values together.
	/// </summary>
	public class AddOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "+"; }
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var left = LeftOperand.GetExpressionType(ctx);
			var right = RightOperand.GetExpressionType(ctx);

			if (left == typeof (string) && left == right)
				return typeof (string);

			return resolveNumericType(ctx);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var type = GetExpressionType(ctx);
			if (type == typeof (string))
			{
				var method = typeof (string).GetMethod("Concat", new[] {typeof (string), typeof (string)});
				LeftOperand.Compile(ctx, true);
				RightOperand.Compile(ctx, true);

				gen.EmitCall(method);
			}
			else
			{
				loadAndConvertNumerics(ctx);
				gen.EmitAdd();
			}
		}
	}
}
