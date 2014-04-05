using System;
using Lens.Compiler;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators
{
	internal class XorOperatorNode : BinaryOperatorNodeBase
	{
		protected override string OperatorRepresentation
		{
			get { return "^^"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_ExclusiveOr"; }
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType == typeof(bool) && rightType == typeof(bool) ? typeof(bool) : null;
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentMethod.Generator;

			if (LeftOperand.Resolve(ctx).IsNumericType())
			{
				loadAndConvertNumerics(ctx);
			}
			else
			{
				LeftOperand.Emit(ctx, true);
				RightOperand.Emit(ctx, true);
			}

			gen.EmitXor();
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return left ^ right;
		}
	}
}
