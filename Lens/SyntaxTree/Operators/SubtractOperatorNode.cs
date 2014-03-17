using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Operators
{
	/// <summary>
	/// An operator node that subtracts a value from another value.
	/// </summary>
	internal class SubtractOperatorNode : BinaryOperatorNodeBase
	{
		public override string OperatorRepresentation
		{
			get { return "-"; }
		}

		public override string OverloadedMethodName
		{
			get { return "op_Subtraction"; }
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType == typeof(string) && rightType == typeof(string) ? typeof(string) : null;
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;
			var type = Resolve(ctx);

			if (type == typeof (string))
			{
				var replaceMethod = typeof (string).GetMethod("Replace", new[] {typeof (string), typeof (string)});

				LeftOperand.Emit(ctx, true);
				RightOperand.Emit(ctx, true);
				gen.EmitConstant(string.Empty);
				gen.EmitCall(replaceMethod);
			}
			else
			{
				loadAndConvertNumerics(ctx);
				gen.EmitSubtract();
			}
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			if (left is string && right is string)
				return left.Replace(right, "");

			return left - right;
		}
	}
}
