using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.Utils;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// A node that represents subscription/unsubscription of an event.
    /// </summary>
    internal class EventNode : NodeBase
    {
        #region Constructor

        public EventNode(EventWrapper evt, SetMemberNode node, bool isSubscription)
        {
            _event = evt;
            _isSubscription = isSubscription;
            _node = node;
            _callback = Expr.CastTransparent(node.Value, _event.EventHandlerType);
        }

        #endregion

        #region Fields

        /// <summary>
        /// Node containing the event expression and name.
        /// </summary>
        private readonly SetMemberNode _node;

        /// <summary>
        /// Callback expression to bind to event.
        /// </summary>
        private NodeBase _callback;

        /// <summary>
        /// Flag indicating that the 
        /// </summary>
        private readonly bool _isSubscription;

        /// <summary>
        /// The event entity.
        /// </summary>
        private readonly EventWrapper _event;

        #endregion

        #region Transform

        protected override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(_callback, x => _callback = x);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            _node.Expression?.EmitNodeForAccess(ctx);

            _callback.Emit(ctx, true);

            var method = _isSubscription ? _event.AddMethod : _event.RemoveMethod;
            gen.EmitCall(method, true);
        }

        #endregion
    }
}