using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Checks that a value fits the type the code generated around it expects, and is where a
    /// value that does not is reported.
    ///
    /// A state machine hands the function's result to a completion source, which is a call the
    /// script never wrote: letting that call report the mismatch describes it as an overload of
    /// TaskCompletionSource that does not exist, naming a type the user has never heard of. The
    /// question is asked here instead, against the expression that answers it.
    /// </summary>
    internal class TypedValueNode : NodeBase
    {
        #region Constructor

        public TypedValueNode(NodeBase value, TypeSignature expected)
        {
            Value = value;
            Expected = expected;

            // the node stands in for the expression it guards, and is reported where it stands
            StartLocation = value.StartLocation;
            EndLocation = value.EndLocation;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The value being handed over.
        /// </summary>
        public readonly NodeBase Value;

        /// <summary>
        /// The type it has to fit. A signature rather than a type, because the machine is built
        /// out of the parse tree, before anything has been resolved.
        /// </summary>
        public readonly TypeSignature Expected;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            var expected = ctx.ResolveType(Expected);

            EnsureLambdaInferred(ctx, Value, expected);

            var actual = Value.Resolve(ctx);
            if (!expected.IsExtendablyAssignableFrom(ctx.Resolver, actual))
                Error(CompilerMessages.ImplicitCastImpossible, actual, expected);

            return actual;
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Value, true);
        }

        // the value may itself suspend - it is the last thing the function does, and awaiting it is
        // the whole point of 'fun fetch:Task<int> -> await (...)' - so the rewrite has to be able to
        // reach through this node to hoist it
        internal override IReadOnlyList<NodeBase> Operands => new[] {Value};

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            return new TypedValueNode(operands[0], Expected) {StartLocation = StartLocation, EndLocation = EndLocation};
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            // the node adds nothing to what is emitted: it only had a question to ask about the
            // value, and binding has answered it by now
            Value.Emit(ctx, mustReturn);
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return $"typed({Expected}, {Value})";
        }

        #endregion
    }
}
