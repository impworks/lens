using System;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.Translations;

namespace Lens.SyntaxTree.Operators.Binary
{
    internal class CoalesceOperatorNode: OperatorNodeBase
    {
        #region Operator basics

        protected override string OperatorRepresentation => "??";

        #endregion

        #region Fields

        /// <summary>
        /// Left expression of the operator (null-checked).
        /// </summary>
        public NodeBase LeftOperand { get; set; }

        /// <summary>
        /// Right expression of the operator (fallback value).
        /// </summary>
        public NodeBase RightOperand { get; set; }

        #endregion

        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            ctx.CheckTypedExpression(LeftOperand, allowNull: true);
            ctx.CheckTypedExpression(RightOperand, allowNull: true);

            var left = LeftOperand.Resolve(ctx);
            var right = RightOperand.Resolve(ctx);

            // no types inferrable
            if (left == typeof(NullType) && right == typeof(NullType))
                return left;

            // only one type known
            if (right == typeof(NullType))
                return left;

            if (left == typeof(NullType))
                return right.IsValueType
                    ? typeof(Nullable<>).MakeGenericType(right)
                    : right;


            if (left.IsValueType && !left.IsNullableType())
                Error(LeftOperand, CompilerMessages.CoalesceOperatorLeftNotNull, left.FullName);

            var baseLeft = left.GetNullableUnderlyingType() ?? left;
            var baseRight = right.GetNullableUnderlyingType() ?? right;

            // do not accept combinations like "nullable<int>" and "string"
            if(baseLeft.IsValueType != baseRight.IsValueType)
                Error(CompilerMessages.CoalesceOperatorTypeMismatch, left.FullName, right.FullName);

            var common = new[] {baseLeft, baseRight}.GetMostCommonType();
            return right.IsNullableType()
                ? typeof(Nullable<>).MakeGenericType(common)
                : common;
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            var left = LeftOperand.Resolve(ctx);
            var right = RightOperand.Resolve(ctx);
            var common = Resolve(ctx);

            var body = Expr.Block();

            var leftAccessor = LeftOperand;
            if (!(LeftOperand is GetIdentifierNode))
            {
                var tmpVar = ctx.Scope.DeclareImplicit(ctx, left, false);
                body.Add(Expr.Set(tmpVar, LeftOperand));
                leftAccessor = Expr.Get(tmpVar);
            }

            var condition = Expr.Compare(ComparisonOperatorKind.Equals, leftAccessor, Expr.Null());
            var leftResult = left.IsNullableType() && left != right
                ? Expr.GetMember(leftAccessor, nameof(Nullable<int>.Value))
                : leftAccessor;

            var rightResult = right.IsNullableType() && left != right
                ? Expr.GetMember(RightOperand, nameof(Nullable<int>.Value))
                : RightOperand;

            body.Add(
                Expr.If(
                    condition,
                    Expr.Block(
                        Expr.Cast(rightResult, common)
                    ),
                    Expr.Block(
                        Expr.Cast(leftResult, common)
                    )
                )
            );
            
            return body;
        }

        #endregion
    }
}
