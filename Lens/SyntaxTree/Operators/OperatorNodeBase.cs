using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Operators
{
    /// <summary>
    /// A base node for all operators.
    /// </summary>
    internal abstract class OperatorNodeBase : NodeBase
    {
        #region Operator basics

        /// <summary>
        /// A textual operator representation for error reporting.
        /// </summary>
        protected abstract string OperatorRepresentation { get; }

        /// <summary>
        /// The name of the method that C# compiler uses for method overloading.
        /// </summary>
        protected virtual string OverloadedMethodName => null;

        /// <summary>
        /// The pointer to overloaded version of the operator.
        /// </summary>
        protected MethodWrapper OverloadedMethod;

        /// <summary>
        /// The user-defined operator binding settled on, if any. An expression tree names it
        /// explicitly rather than letting the Expression API look it up again.
        /// </summary>
        internal MethodWrapper BoundOperatorMethod => OverloadedMethod;

        /// <summary>
        /// How the operator is spelled in the source, for diagnostics.
        /// </summary>
        internal string Representation => OperatorRepresentation;

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            var result = IsConstant && ctx.Options.UnrollConstants
                ? Expr.Constant(ConstantValue)
                : null;

            return result;
        }

        #endregion
    }
}