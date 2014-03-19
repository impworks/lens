using System;
using Lens.Compiler;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators
{
	internal class BitOperatorNode : BinaryOperatorNodeBase
	{
		public BitOperatorNode(LogicalOperatorKind kind = default(LogicalOperatorKind))
		{
			Kind = kind;
		}

		/// <summary>
		/// The kind of boolean operator.
		/// </summary>
		public LogicalOperatorKind Kind { get; set; }

		protected override string OperatorRepresentation
		{
			get
			{
				return Kind == LogicalOperatorKind.And ? "&" : (Kind == LogicalOperatorKind.Or ? "|" : "^");
			}
		}

		protected override string OverloadedMethodName
		{
			get
			{
				return Kind == LogicalOperatorKind.And ? "op_BinaryAnd" : (Kind == LogicalOperatorKind.Or ? "op_BinaryOr" : "op_ExclusiveOr");
			}
		}

		protected override Type resolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
			return leftType == rightType && leftType.IsIntegerType() ? leftType : null;
		}

		protected override void compileOperator(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;

			LeftOperand.Emit(ctx, true);
			RightOperand.Emit(ctx, true);

			if(Kind == LogicalOperatorKind.And)
				gen.EmitAnd();
			else if (Kind == LogicalOperatorKind.Or)
				gen.EmitOr();
			else
				gen.EmitXor();
		}

		protected override dynamic unrollConstant(dynamic left, dynamic right)
		{
			return Kind == LogicalOperatorKind.And ? left & right : (Kind == LogicalOperatorKind.Or ? left | right : left ^ right);
		}
	}
}
