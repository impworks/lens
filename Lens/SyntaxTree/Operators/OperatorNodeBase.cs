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