using System;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators.Binary
{
	internal class XorOperatorNode : BinaryOperatorNodeBase
	{
		#region Operator basics

		protected override string OperatorRepresentation
		{
			get { return "^^"; }
		}

		protected override string OverloadedMethodName
		{
			get { return "op_ExclusiveOr"; }
		}

		#endregion

		#region Resolve

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType == typeof(bool) && rightType == typeof(bool) ? typeof(bool) : null;
		}

		#endregion

		#region Emit

		protected override void emitOperator(Context ctx)
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

		#endregion

		#region Constant unroll

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return left ^ right;
		}

		#endregion
	}
}
