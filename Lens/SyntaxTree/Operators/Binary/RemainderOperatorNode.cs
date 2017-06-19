using Lens.Compiler;

namespace Lens.SyntaxTree.Operators.Binary
{
	/// <summary>
	/// An operator node that divides one value by another value.
	/// </summary>
	internal class RemainderOperatorNode : BinaryOperatorNodeBase
	{
		#region Operator basics

		protected override string OperatorRepresentation => "%";

	    protected override string OverloadedMethodName => "op_Modulus";

	    protected override BinaryOperatorNodeBase RecreateSelfWithArgs(NodeBase left, NodeBase right)
        {
            return new RemainderOperatorNode { LeftOperand = left, RightOperand = right };
        }

		#endregion

		#region Emit

		protected override void EmitOperator(Context ctx)
		{
			LoadAndConvertNumerics(ctx);
			ctx.CurrentMethod.Generator.EmitRemainder();
		}

		#endregion

		#region Constant unroll

		protected override dynamic UnrollConstant(dynamic left, dynamic right)
		{
			return left % right;
		}

		#endregion
	}
}
