using System;
using Lens.Compiler;
using Lens.Translations;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that adds two values together.
	/// </summary>
	public class AddOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "+"; }
		}

		public override string OverloadedMethodName
		{
			get { return "op_Addition"; }
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType == typeof (string) && rightType == typeof (string) ? typeof (string) : null;
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			var type = GetExpressionType(ctx);
			if (type == typeof (string))
			{
				var method = typeof (string).GetMethod("Concat", new[] {typeof (string), typeof (string)});
				LeftOperand.Compile(ctx, true);
				RightOperand.Compile(ctx, true);

				gen.EmitCall(method);
			}
			else
			{
				loadAndConvertNumerics(ctx);
				gen.EmitAdd();
			}
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			try
			{
				return checked(left + right);
			}
			catch (OverflowException)
			{
				Error(CompilerMessages.ConstantOverflow);
				return null;
			}
		}
	}
}
