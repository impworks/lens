using System;
using Lens.Compiler;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the unary numeric negation operation.
	/// </summary>
	internal class NegationOperatorNode : UnaryOperatorNodeBase
	{
		protected override string OperatorRepresentation
		{
			get { return "-"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_UnaryNegation"; }
		}

		protected override Type resolveOperatorType(Context ctx)
		{
			var type = Operand.Resolve(ctx);
			return type.IsNumericType() ? type : null;
		}

		protected override void compileOperator(Context ctx)
		{
			Operand.Emit(ctx, true);
			ctx.CurrentILGenerator.EmitNegate();
		}

		protected override dynamic unrollConstant(dynamic value)
		{
			return -value;
		}
	}
}
