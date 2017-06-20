using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Operators.Unary
{
    /// <summary>
    /// The base for all unary operators.
    /// </summary>
    internal abstract class UnaryOperatorNodeBase : OperatorNodeBase
    {
        #region Fields

        /// <summary>
        /// The operand.
        /// </summary>
        public NodeBase Operand { get; set; }

        #endregion

        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            var type = Operand.Resolve(ctx);

            var result = ResolveOperatorType(ctx);
            if (result != null)
                return result;

            if (OverloadedMethodName != null)
            {
                try
                {
                    OverloadedMethod = ctx.ResolveMethod(type, OverloadedMethodName, new[] {type});

                    // cannot be generic
                    if (OverloadedMethod != null)
                        return OverloadedMethod.ReturnType;
                }
                catch
                {
                }
            }

            Error(CompilerMessages.OperatorUnaryTypeMismatch, OperatorRepresentation, type);
            return null;
        }

        /// <summary>
        /// Overridable resolver for unary operators.
        /// </summary>
        protected virtual Type ResolveOperatorType(Context ctx)
        {
            return null;
        }

        #endregion

        #region Transform

        protected override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Operand, x => Operand = x);
        }

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
            Expr.Cast(Operand, ps[0]).Emit(ctx, true);
            gen.EmitCall(OverloadedMethod.MethodInfo);
        }

        protected abstract void EmitOperator(Context ctx);

        #endregion

        #region Constant unroll

        public override bool IsConstant => Operand.IsConstant;
        public override dynamic ConstantValue => UnrollConstant(Operand.ConstantValue);

        /// <summary>
        /// Overriddable constant unroller for unary operators.
        /// </summary>
        protected abstract dynamic UnrollConstant(dynamic value);

        #endregion

        #region Debug

        protected bool Equals(UnaryOperatorNodeBase other)
        {
            return Equals(Operand, other.Operand);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((UnaryOperatorNodeBase) obj);
        }

        public override int GetHashCode()
        {
            return (Operand != null ? Operand.GetHashCode() : 0);
        }

        public override string ToString()
        {
            return string.Format("op{0}({1})", OperatorRepresentation, Operand);
        }

        #endregion
    }
}