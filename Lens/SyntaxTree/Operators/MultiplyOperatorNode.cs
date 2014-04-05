using System;
using Lens.Utils;
using Lens.Compiler;
using Lens.Translations;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that multiplies one value by another value.
	/// </summary>
	internal class MultiplyOperatorNode : BinaryOperatorNodeBase
	{
		protected override string OperatorRepresentation
		{
			get { return "*"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_Multiply"; }
		}

		public override NodeBase Expand(Context ctx, bool mustReturn)
		{
			return mathExpansion(LeftOperand, RightOperand) ?? mathExpansion(RightOperand, LeftOperand);
		}

		protected override void compileOperator(Context ctx)
		{
			loadAndConvertNumerics(ctx);
			ctx.CurrentMethod.Generator.EmitMultiply();
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			try
			{
				return checked(left * right);
			}
			catch (OverflowException)
			{
				error(CompilerMessages.ConstantOverflow);
				return null;
			}
		}

		private static NodeBase mathExpansion(NodeBase one, NodeBase other)
		{
			if (one.IsConstant)
			{
				var value = one.ConstantValue;
				if (value == 0) return Expr.Int(0);
				if (value == 1) return other;
			}

			return null;
		}
	}
}
