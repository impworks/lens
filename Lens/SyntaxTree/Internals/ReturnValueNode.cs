using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Utils;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Leaves the current method with a value.
    ///
    /// LENS has no return statement: a function's value is the value of its body. A lowered state
    /// machine does need one, because MoveNext leaves from wherever the resume point happens to be.
    /// </summary>
    internal class ReturnValueNode : NodeBase, IMetaNode
    {
        #region Constructor

        public ReturnValueNode(NodeBase value, TypeEntry returnType = null)
        {
            Value = value;
            ReturnType = returnType;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The value to leave with, or null to leave a void method.
        /// </summary>
        public readonly NodeBase Value;

        /// <summary>
        /// The type the value must be converted to, if it is not already of it.
        /// </summary>
        public readonly TypeEntry ReturnType;

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            if (Value != null)
                yield return new NodeChild(Value);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            if (Value != null)
            {
                var node = ReturnType != null && !ReturnType.Equals(Value.Resolve(ctx))
                    ? Expr.Cast(Value, ReturnType)
                    : Value;

                node.Emit(ctx, true);
            }

            gen.EmitReturn();
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return Value == null ? "return" : $"return {Value}";
        }

        #endregion
    }
}
