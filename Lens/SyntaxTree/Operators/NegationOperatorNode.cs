using System;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// A node representing the unary numeric negation operation.
	/// </summary>
	internal class NegationOperatorNode : UnaryOperatorNodeBase
	{
		#region Operator basics

		protected override string OperatorRepresentation
		{
			get { return "-"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_UnaryNegation"; }
		}

		#endregion

		#region Resolve

		protected override Type resolveOperatorType(Context ctx)
		{
			var type = Operand.Resolve(ctx);
			return type.IsNumericType() ? type : null;
		}

		#endregion

		#region Transform

		protected override NodeBase expand(Context ctx, bool mustReturn)
		{
			// double negation
			var op = Operand as NegationOperatorNode;
			if (op != null)
				return op.Operand;

			return base.expand(ctx, mustReturn);
		}

		#endregion

		#region Emit

		protected override void emitOperator(Context ctx)
		{
			Operand.Emit(ctx, true);
			ctx.CurrentMethod.Generator.EmitNegate();
		}

		#endregion

		#region Constant unroll

		protected override dynamic unrollConstant(dynamic value)
		{
			return -value;
		}

		#endregion
	}
}
