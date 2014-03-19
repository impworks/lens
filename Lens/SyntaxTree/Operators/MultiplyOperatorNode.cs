using System;
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

		protected override void compileOperator(Context ctx)
		{
			loadAndConvertNumerics(ctx);
			ctx.CurrentILGenerator.EmitMultiply();
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
	}
}
