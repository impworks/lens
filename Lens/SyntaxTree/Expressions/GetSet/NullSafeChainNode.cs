using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.GetSet
{
    /// <summary>
    /// A wrapper around an accessor chain that contains at least one null-safe accessor.
    ///
    /// The short-circuiting cannot live on the accessors themselves, because a null receiver must
    /// skip the whole remainder of the chain rather than a single link: "a?.b.c" evaluates neither
    /// ".b" nor ".c" when "a" is null. Therefore the parser hands the entire chain over to this node,
    /// which lowers it into a sequence of null checks over temporary locals:
    ///
    ///     let tmp = a
    ///     if tmp == null then default(T?) else tmp.b.c
    /// </summary>
    internal class NullSafeChainNode : NodeBase
    {
        #region Fields

        /// <summary>
        /// The outermost node of the wrapped chain.
        /// </summary>
        public NodeBase Chain { get; set; }

        /// <summary>
        /// Null checks to be performed, ordered from the innermost to the outermost.
        /// </summary>
        private List<NullCheck> _checks;

        /// <summary>
        /// The type of the chain's value when none of the checks trip.
        /// </summary>
        private Type _chainType;

        #endregion

        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            PrepareChain(ctx);

            // "foo?.DoStuff ()" is a no-op rather than a value
            if (_chainType.IsVoid())
                return typeof(UnitType);

            // a value type gains a null state by being lifted into Nullable<T>, but only once
            return _chainType.IsValueType && !_chainType.IsNullableType()
                ? typeof(Nullable<>).MakeGenericType(_chainType)
                : _chainType;
        }

        /// <summary>
        /// Finds the receivers that must be null-checked and validates their types.
        /// A receiver of the Nullable&lt;T&gt; type is rerouted through its Value property:
        /// the rest of the chain accesses members of T, and comparing a Nullable&lt;T&gt;
        /// against null would box it.
        /// </summary>
        private void PrepareChain(Context ctx)
        {
            if (_checks != null)
                return;

            _checks = new List<NullCheck>();

            var links = Flatten(Chain);
            for (var idx = 1; idx < links.Count; idx++)
            {
                var accessor = links[idx] as AccessorNodeBase;
                if (accessor == null || !accessor.IsNullSafe)
                    continue;

                var receiver = links[idx - 1];
                var receiverType = receiver.Resolve(ctx);
                var isNullable = receiverType.IsNullableType();

                if (receiverType.IsValueType && !isNullable)
                    Error(receiver, CompilerMessages.NullSafeOperatorValueType, receiverType.FullName);

                var holder = accessor;
                if (isNullable)
                {
                    var valueGetter = Expr.GetMember(receiver, nameof(Nullable<int>.Value));
                    accessor.Expression = valueGetter;
                    holder = valueGetter;
                }

                _checks.Add(new NullCheck
                {
                    Holder = holder,
                    Receiver = receiver,
                    ReceiverType = receiverType,
                    IsNullable = isNullable
                });
            }

            _chainType = Chain.Resolve(ctx);
        }

        /// <summary>
        /// Returns the nodes of the chain, from its root expression to its outermost accessor.
        /// </summary>
        private static List<NodeBase> Flatten(NodeBase chain)
        {
            var links = new List<NodeBase>();

            var curr = chain;
            while (curr != null)
            {
                links.Add(curr);
                curr = GetReceiver(curr);
            }

            links.Reverse();
            return links;
        }

        /// <summary>
        /// Returns the expression the given link is applied to, if it is a link at all.
        /// </summary>
        private static NodeBase GetReceiver(NodeBase node)
        {
            if (node is AccessorNodeBase accessor)
                return accessor.Expression;

            // an invocation is a transparent link: "a?.b x" invokes the member accessed by the chain
            return (node as InvocationNode)?.Expression;
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            var resultType = Resolve(ctx);
            var isUnit = resultType.IsVoid();

            // each checked receiver is evaluated once, into a local the rest of the chain reads from
            foreach (var check in _checks)
            {
                check.Local = ctx.Scope.DeclareImplicit(ctx, check.ReceiverType, false);
                check.Holder.Expression = Expr.Get(check.Local);
            }

            var body = isUnit || _chainType == resultType
                ? Chain
                : Expr.Cast(Chain, resultType);

            for (var idx = _checks.Count - 1; idx >= 0; idx--)
            {
                var check = _checks[idx];
                body = Expr.Block(
                    Expr.Set(check.Local, check.Receiver),
                    isUnit
                        ? Expr.If(IsNotNull(check), Expr.Block(body))
                        : Expr.If(IsNull(check), Expr.Block(Expr.Default(resultType)), Expr.Block(body))
                );
            }

            return body;
        }

        protected override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Chain);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Creates the condition that holds when the checked receiver is null.
        /// </summary>
        private static NodeBase IsNull(NullCheck check)
        {
            return check.IsNullable
                ? Expr.Not(Expr.GetMember(Expr.Get(check.Local), nameof(Nullable<int>.HasValue)))
                : (NodeBase) Expr.Equal(Expr.Get(check.Local), Expr.Null());
        }

        /// <summary>
        /// Creates the condition that holds when the checked receiver is not null.
        /// </summary>
        private static NodeBase IsNotNull(NullCheck check)
        {
            return check.IsNullable
                ? Expr.GetMember(Expr.Get(check.Local), nameof(Nullable<int>.HasValue))
                : (NodeBase) Expr.NotEqual(Expr.Get(check.Local), Expr.Null());
        }

        /// <summary>
        /// A single null check of the chain.
        /// </summary>
        private class NullCheck
        {
            /// <summary>
            /// The node whose Expression property holds the receiver.
            /// It is the null-safe accessor itself, unless a Value getter has been inserted.
            /// </summary>
            public AccessorNodeBase Holder;

            /// <summary>
            /// The sub-chain that produces the value being checked.
            /// </summary>
            public NodeBase Receiver;

            /// <summary>
            /// The type of the checked value.
            /// </summary>
            public Type ReceiverType;

            /// <summary>
            /// Flag indicating that the checked value is a Nullable&lt;T&gt;.
            /// </summary>
            public bool IsNullable;

            /// <summary>
            /// The local the checked value is saved to.
            /// </summary>
            public Local Local;
        }

        #endregion

        #region Debug

        protected bool Equals(NullSafeChainNode other)
        {
            return Equals(Chain, other.Chain);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((NullSafeChainNode) obj);
        }

        public override int GetHashCode()
        {
            return Chain != null ? Chain.GetHashCode() : 0;
        }

        public override string ToString()
        {
            return string.Format("nullsafe({0})", Chain);
        }

        #endregion
    }
}
