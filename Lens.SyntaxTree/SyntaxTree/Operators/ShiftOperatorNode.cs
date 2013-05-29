using System;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Operators
{
	public class ShiftOperatorNode : BinaryOperatorNodeBase
	{
		public bool IsLeft { get; set; }

		public override string OperatorRepresentation
		{
			get { return IsLeft ? "<:" : ":>"; }
		}

		public override string OverloadedMethodName
		{
			get { return IsLeft ? "op_LeftShift" : "op_RightShift"; }
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType.IsAnyOf(typeof (int), typeof (long)) && rightType == typeof (int) ? leftType : null;
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			LeftOperand.Compile(ctx, true);
			RightOperand.Compile(ctx, true);

			if (IsLeft)
				gen.EmitShiftLeft();
			else
				gen.EmitShiftRight();
		}
	}
}
