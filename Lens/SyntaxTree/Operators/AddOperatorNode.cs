using System;
using Lens.Compiler;
using Lens.Translations;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that adds two values together.
	/// </summary>
	internal class AddOperatorNode : BinaryOperatorNodeBase
	{
		protected override string OperatorRepresentation
		{
			get { return "+"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_Addition"; }
		}

		public override NodeBase Expand(Context ctx, bool mustReturn)
		{
			if (!IsConstant)
			{
				if(Resolve(ctx) == typeof(string))
					return Expr.Invoke("string", "Concat", LeftOperand, RightOperand);
			}

			return mathExpand(LeftOperand, RightOperand) ?? mathExpand(RightOperand, LeftOperand);
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType == typeof (string) && rightType == typeof (string) ? typeof (string) : null;
		}

		protected override void compileOperator(Context ctx)
		{
			loadAndConvertNumerics(ctx);
			ctx.CurrentILGenerator.EmitAdd();
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			try
			{
				return checked(left + right);
			}
			catch (OverflowException)
			{
				error(CompilerMessages.ConstantOverflow);
				return null;
			}
		}

		private static NodeBase mathExpand(NodeBase one, NodeBase other)
		{
			if (one.IsConstant && one.ConstantValue == 0)
				return other;

			return null;
		}
	}
}
