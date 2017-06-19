using System;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators.Binary
{
    using Lens.Translations;


    internal class BitOperatorNode : BinaryOperatorNodeBase
	{
		#region Constructor

		public BitOperatorNode(LogicalOperatorKind kind = default(LogicalOperatorKind))
		{
			Kind = kind;
		}

		#endregion

		#region Fields

		/// <summary>
		/// The kind of boolean operator.
		/// </summary>
		public LogicalOperatorKind Kind { get; set; }

		#endregion

		#region Operator basics

		protected override string OperatorRepresentation => Kind == LogicalOperatorKind.And ? "&" : (Kind == LogicalOperatorKind.Or ? "|" : "^");

	    protected override string OverloadedMethodName => Kind == LogicalOperatorKind.And ? "op_BinaryAnd" : (Kind == LogicalOperatorKind.Or ? "op_BinaryOr" : "op_ExclusiveOr");

	    protected override BinaryOperatorNodeBase RecreateSelfWithArgs(NodeBase left, NodeBase right)
	    {
	        return new BitOperatorNode(Kind) {LeftOperand = left, RightOperand = right};
	    }

	    #endregion

		#region Resolve

		protected override Type ResolveOperatorType(Context ctx, Type leftType, Type rightType)
		{
		    if (leftType == rightType)
		    {
		        if (leftType.IsIntegerType() || leftType.IsEnum)
		            return leftType;
		    }

			Error(CompilerMessages.OperatorBinaryTypesMismatch, OperatorRepresentation, leftType, rightType);
		    return null;
		}

		#endregion

		#region Emit

		protected override void EmitOperator(Context ctx)
		{
			var gen = ctx.CurrentMethod.Generator;

			LeftOperand.Emit(ctx, true);
			RightOperand.Emit(ctx, true);

			if(Kind == LogicalOperatorKind.And)
				gen.EmitAnd();
			else if (Kind == LogicalOperatorKind.Or)
				gen.EmitOr();
			else
				gen.EmitXor();
		}

		#endregion

		#region Constant unroll

		protected override dynamic UnrollConstant(dynamic left, dynamic right)
		{
			return Kind == LogicalOperatorKind.And
				? left & right
				: (Kind == LogicalOperatorKind.Or
					? left | right
					: left ^ right
				  );
		}

		#endregion
	}
}
