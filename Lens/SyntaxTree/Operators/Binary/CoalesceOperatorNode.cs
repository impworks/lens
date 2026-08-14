using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.Translations;
using Lens.Utils;

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

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            ctx.CheckTypedExpression(LeftOperand, allowNull: true);
            ctx.CheckTypedExpression(RightOperand, allowNull: true);

            var left = LeftOperand.Resolve(ctx);
            var right = RightOperand.Resolve(ctx);

            // no types inferrable
            if (left.Is<NullType>() && right.Is<NullType>())
                return left;

            // only one type known
            if (right.Is<NullType>())
                return left;

            if (left.Is<NullType>())
                return right.IsValueType
                    ? TypeEntryCache.Of(typeof(Nullable<>)).MakeGeneric(ctx.Resolver, new[] {right})
                    : right;


            if (left.IsValueType && !left.IsNullableType())
                Error(LeftOperand, CompilerMessages.CoalesceOperatorLeftNotNull, left.FullName);

            var baseLeft = left.GetNullableUnderlyingType() ?? left;
            var baseRight = right.GetNullableUnderlyingType() ?? right;

            // do not accept combinations like "nullable<int>" and "string"
            if(baseLeft.IsValueType != baseRight.IsValueType)
                Error(CompilerMessages.CoalesceOperatorTypeMismatch, left.FullName, right.FullName);

            var common = new[] {baseLeft, baseRight}.GetMostCommonType(ctx.Resolver);
            return right.IsNullableType()
                ? TypeEntryCache.Of(typeof(Nullable<>)).MakeGeneric(ctx.Resolver, new[] {common})
                : common;
        }

        #endregion

        #region Transform

        /// <summary>
        /// The node expands into the branch it stands for, and binding transforms that instead of
        /// this - so these are here for the passes that read the tree before binding rather than
        /// for binding itself. The operands are deliberately not reported as this node's operands:
        /// the fallback is evaluated only when the first operand turned out to be null, and one
        /// evaluated ahead of time would be evaluated when the source says it should not be.
        /// </summary>
        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(LeftOperand);
            yield return new NodeChild(RightOperand);
        }

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
                        Expr.Cast(rightResult, common.Materialize())
                    ),
                    Expr.Block(
                        Expr.Cast(leftResult, common.Materialize())
                    )
                )
            );
            
            return body;
        }

        #endregion
    }
}
