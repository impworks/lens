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
		public override string OperatorRepresentation
		{
			get { return "*"; }
		}

		public override string OverloadedMethodName
		{
			get { return "op_Multiply"; }
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;
			GetExpressionType(ctx);
			loadAndConvertNumerics(ctx);
			gen.EmitMultiply();
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			try
			{
				return checked(left * right);
			}
			catch (OverflowException)
			{
				Error(CompilerMessages.ConstantOverflow);
				return null;
			}
		}
	}
}
