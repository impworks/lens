using System;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that divides one value by another value.
	/// </summary>
	public class DivideOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "/"; }
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return resolveNumericType(ctx);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			if(LeftOperand.GetExpressionType(ctx).IsIntegerType() && RightOperand is IntNode && (RightOperand as IntNode).Value == 0)
				Error("Constant division by zero!");
			
			var gen = ctx.CurrentILGenerator;

			GetExpressionType(ctx);
			loadAndConvertNumerics(ctx);

			gen.EmitDivide();
		}
	}
}
