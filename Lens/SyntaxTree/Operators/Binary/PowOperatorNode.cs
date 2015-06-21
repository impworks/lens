using System;
using System.Reflection;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators.Binary
{
	/// <summary>
	/// An operator node that raises one value to the power of another value.
	/// </summary>
	internal class PowOperatorNode : BinaryOperatorNodeBase
	{
		#region Constants

		private static readonly MethodInfo _PowMethod = typeof(Math).GetMethod("Pow", new[] { typeof(double), typeof(double) });

		#endregion

		#region Operator basics

		protected override string OperatorRepresentation
		{
			get { return "**"; }
		}

        protected override BinaryOperatorNodeBase recreateSelfWithArgs(NodeBase left, NodeBase right)
        {
            return new PowOperatorNode { LeftOperand = left, RightOperand = right };
        }

		#endregion

		#region Resolve

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType.IsNumericType() && rightType.IsNumericType() ? typeof (double) : null;
		}

		#endregion

		#region Emit

		protected override void emitOperator(Context ctx)
		{
			if (RightOperand.IsConstant && RightOperand.ConstantValue is int)
			{
				var constPower = (int)RightOperand.ConstantValue;
				if(constPower > 0 && constPower <= 10)
				{
					var gen = ctx.CurrentMethod.Generator;

					// detect maximum power of 2 inside current power
					var squareCount = 0;
					var powerOf2 = 1;
					while (constPower - powerOf2 >= powerOf2)
					{
						powerOf2 *= 2;
						squareCount ++;
					}

					var multCount = constPower - powerOf2;

					LeftOperand.Emit(ctx, true);
					gen.EmitConvert(typeof(double));

					for (var i = 0; i < multCount; i++)
						gen.EmitDup();

					for (var i = 0; i < squareCount; i++)
					{
						gen.EmitDup();
						gen.EmitMultiply();
					}

					for (var i = 0; i < multCount; i++)
						gen.EmitMultiply();

					return;
				}
			}

			loadAndConvertNumerics(ctx, typeof(double));
			ctx.CurrentMethod.Generator.EmitCall(_PowMethod);
		}

		#endregion

		#region Constant unroll

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return Math.Pow(left, right);
		}

		#endregion
	}
}
