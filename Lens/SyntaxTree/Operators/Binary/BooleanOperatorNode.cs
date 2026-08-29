using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators.Binary
{
    /// <summary>
    /// A node representing AND / OR binary operations.
    /// </summary>
    internal class BooleanOperatorNode : BinaryOperatorNodeBase
    {
        #region Constructor

        public BooleanOperatorNode(LogicalOperatorKind kind = default(LogicalOperatorKind))
        {
            if (kind == LogicalOperatorKind.Xor)
                throw new ArgumentException("Use XorOperatorNode to represent a XOR ");

            Kind = kind;
        }

        #endregion

        #region Fields

        public LogicalOperatorKind Kind;

        #endregion

        #region Operator basics

        protected override bool IsNumericOperator => false;

        protected override string OperatorRepresentation => Kind == LogicalOperatorKind.And ? "&&" : "||";

        protected override BinaryOperatorNodeBase RecreateSelfWithArgs(NodeBase left, NodeBase right)
        {
            return new BooleanOperatorNode(Kind) {LeftOperand = left, RightOperand = right};
        }

        /// <summary>
        /// The right operand is evaluated only when the left one did not already decide the answer,
        /// so neither operand may be hoisted out of the operator. A rewrite that needs to suspend
        /// inside one has to turn the operator into the branch it stands for first.
        /// </summary>
        internal override IReadOnlyList<NodeBase> Operands => NoOperands;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveOperatorType(Context ctx, TypeEntry leftType, TypeEntry rightType)
        {
            return leftType.IsImplicitlyBoolean() && rightType.IsImplicitlyBoolean()
                ? TypeEntryCache.Of<bool>()
                : null;
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            // folding gives the best code there is, but it can be switched off - and an operator
            // whose meaning is a call rather than an opcode has to become that call either way
            var folded = base.Expand(ctx, mustReturn);
            if (folded != null)
                return folded;

            // there is no opcode for a short-circuiting operator: it is a branch, and stays one
            // even when both its operands turned out to be constants
            return Kind == LogicalOperatorKind.And
                ? Expr.If(LeftOperand, Expr.Block(Expr.Cast<bool>(RightOperand)), Expr.Block(Expr.False()))
                : Expr.If(LeftOperand, Expr.Block(Expr.True()), Expr.Block(Expr.Cast<bool>(RightOperand)));
        }

        protected override void EmitOperator(Context ctx)
        {
            throw new InvalidOperationException("The BooleanOperatorNode has not been expanded!");
        }

        #endregion

        #region Constant unroll

        protected override dynamic UnrollConstant(dynamic left, dynamic right)
        {
            return Kind == LogicalOperatorKind.And ? left && right : left || right;
        }

        #endregion
    }

    /// <summary>
    /// The kind of bit or boolean operators.
    /// </summary>
    public enum LogicalOperatorKind
    {
        And,
        Or,
        Xor
    }
}