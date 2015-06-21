using Lens.Compiler;

namespace Lens.SyntaxTree.Operators.Binary
{
	/// <summary>
	/// An operator node that divides one value by another value.
	/// </summary>
	internal class RemainderOperatorNode : BinaryOperatorNodeBase
	{
		#region Operator basics

		protected override string OperatorRepresentation
		{
			get { return "%"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_Modulus"; }
		}

        protected override BinaryOperatorNodeBase recreateSelfWithArgs(NodeBase left, NodeBase right)
        {
            return new RemainderOperatorNode { LeftOperand = left, RightOperand = right };
        }

		#endregion

		#region Emit

		protected override void emitOperator(Context ctx)
		{
			loadAndConvertNumerics(ctx);
			ctx.CurrentMethod.Generator.EmitRemainder();
		}

		#endregion

		#region Constant unroll

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return left % right;
		}

		#endregion
	}
}
