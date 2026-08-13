using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators.Binary
{
    /// <summary>
    /// The base for all binary operators.
    /// </summary>
    internal abstract class BinaryOperatorNodeBase : OperatorNodeBase
    {
        #region Fields

        /// <summary>
        /// The operand to the left side.
        /// </summary>
        public NodeBase LeftOperand { get; set; }

        /// <summary>
        /// The operand to the right side.
        /// </summary>
        public NodeBase RightOperand { get; set; }

        /// <summary>
        /// Checks if numeric type casting checks should be applied to operands.
        /// </summary>
        protected virtual bool IsNumericOperator => true;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            var leftType = LeftOperand.Resolve(ctx);
            var rightType = RightOperand.Resolve(ctx);

            var result = ResolveOperatorType(ctx, leftType, rightType);
            if (result != null)
                return result;

            if (OverloadedMethodName != null)
            {
                try
                {
                    OverloadedMethod = ctx.ResolveMethod(leftType, OverloadedMethodName, new[] {leftType, rightType});
                }
                catch
                {
                    try
                    {
                        OverloadedMethod = ctx.ResolveMethod(rightType, OverloadedMethodName, new[] {leftType, rightType});
                    }
                    catch
                    {
                    }
                }

                // cannot be generic
                if (OverloadedMethod != null)
                    return OverloadedMethod.ReturnType;
            }

            if (IsNumericOperator)
            {
                if (leftType.IsNullableType() || rightType.IsNullableType())
                {
                    var leftNullable = leftType.IsNullableType() ? leftType.GenericArguments[0] : leftType;
                    var rightNullable = rightType.IsNullableType() ? rightType.GenericArguments[0] : rightType;

                    var commonNumericType = TypeExtensions.GetNumericOperationType(leftNullable, rightNullable);
                    if (commonNumericType == null)
                        Error(CompilerMessages.OperatorTypesSignednessMismatch);

                    return TypeEntryCache.Of(typeof(Nullable<>)).MakeGeneric(ctx.Resolver, new[] {commonNumericType});
                }

                if (leftType.IsNumericType() && rightType.IsNumericType())
                {
                    var commonNumericType = TypeExtensions.GetNumericOperationType(leftType, rightType);
                    if (commonNumericType == null)
                        Error(CompilerMessages.OperatorTypesSignednessMismatch);

                    return commonNumericType;
                }
            }

            Error(this, CompilerMessages.OperatorBinaryTypesMismatch, OperatorRepresentation, leftType, rightType);
            return null;
        }

        /// <summary>
        /// Resolves operator return type, in case it's not an overloaded method call.
        /// </summary>
        protected virtual TypeEntry ResolveOperatorType(Context ctx, TypeEntry leftType, TypeEntry rightType)
        {
            return null;
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(LeftOperand);
            yield return new NodeChild(RightOperand);
        }

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            if (Resolve(ctx).IsNullableType())
            {
                var leftNullable = LeftOperand.Resolve(ctx).IsNullableType();
                var rightNullable = RightOperand.Resolve(ctx).IsNullableType();
                if (leftNullable && rightNullable)
                {
                    return Expr.If(
                        Expr.And(
                            Expr.GetMember(LeftOperand, "HasValue"),
                            Expr.GetMember(RightOperand, "HasValue")
                        ),
                        Expr.Block(
                            RecreateSelfWithArgs(
                                Expr.GetMember(LeftOperand, "Value"),
                                Expr.GetMember(RightOperand, "Value")
                            )
                        ),
                        Expr.Block(Expr.Null())
                    );
                }

                if (leftNullable)
                {
                    return Expr.If(
                        Expr.GetMember(LeftOperand, "HasValue"),
                        Expr.Block(
                            RecreateSelfWithArgs(
                                Expr.GetMember(LeftOperand, "Value"),
                                RightOperand
                            )
                        ),
                        Expr.Block(Expr.Null())
                    );
                }

                if (rightNullable)
                {
                    return Expr.If(
                        Expr.GetMember(RightOperand, "HasValue"),
                        Expr.Block(
                            RecreateSelfWithArgs(
                                LeftOperand,
                                Expr.GetMember(RightOperand, "Value")
                            )
                        ),
                        Expr.Block(Expr.Null())
                    );
                }
            }

            return base.Expand(ctx, mustReturn);
        }

        protected abstract BinaryOperatorNodeBase RecreateSelfWithArgs(NodeBase left, NodeBase right);

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            if (OverloadedMethod == null)
            {
                EmitOperator(ctx);
                return;
            }

            var ps = OverloadedMethod.ArgumentTypes;
            Expr.Cast(LeftOperand, ps[0].Materialize()).Emit(ctx, true);
            Expr.Cast(RightOperand, ps[1].Materialize()).Emit(ctx, true);
            gen.EmitCall(OverloadedMethod.MethodInfo);
        }

        /// <summary>
        /// Emits operator code, in case it's not an overloaded method call.
        /// </summary>
        protected abstract void EmitOperator(Context ctx);

        #endregion

        #region Helper methods

        /// <summary>
        /// Loads both arguments and converts them to the biggest common type.
        /// </summary>
        protected void LoadAndConvertNumerics(Context ctx, Type type = null)
        {
            var left = LeftOperand.Resolve(ctx);
            var right = RightOperand.Resolve(ctx);

            if (type == null)
                type = TypeExtensions.GetNumericOperationType(left, right)?.Materialize();

            Expr.Cast(LeftOperand, type).Emit(ctx, true);
            Expr.Cast(RightOperand, type).Emit(ctx, true);
        }

        #endregion

        #region Constant unroll

        public override bool IsConstant => RightOperand.IsConstant && LeftOperand.IsConstant;
        public override object ConstantValue => UnrollConstant(LeftOperand.ConstantValue, RightOperand.ConstantValue);

        /// <summary>
        /// Calculates the constant value in compile time.
        /// </summary>
        protected abstract dynamic UnrollConstant(dynamic left, dynamic right);

        #endregion

        #region Debug

        protected bool Equals(BinaryOperatorNodeBase other)
        {
            return Equals(LeftOperand, other.LeftOperand) && Equals(RightOperand, other.RightOperand);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((BinaryOperatorNodeBase) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((LeftOperand != null ? LeftOperand.GetHashCode() : 0) * 397) ^ (RightOperand != null ? RightOperand.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return string.Format("op{0}({1}, {2})", OperatorRepresentation, LeftOperand, RightOperand);
        }

        #endregion
    }
}