using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.Utils;

namespace Lens.SyntaxTree.Internals
{
	internal class EventNode : NodeBase
	{
		#region Constructor

		public EventNode(EventWrapper evt, SetMemberNode node, bool isSubscription)
		{
			_Event = evt;
			_IsSubscription = isSubscription;
			_Node = node;
			_Callback = Expr.CastTransparent(node.Value, _Event.EventHandlerType);
		}

		#endregion

		#region Fields

		/// <summary>
		/// Node containing the event expression and name.
		/// </summary>
		private readonly SetMemberNode _Node;

		/// <summary>
		/// Callback expression to bind to event.
		/// </summary>
		private NodeBase _Callback;

		/// <summary>
		/// Flag indicating that the 
		/// </summary>
		private readonly bool _IsSubscription;

		/// <summary>
		/// The event entity.
		/// </summary>
		private readonly EventWrapper _Event;

		#endregion

		#region Transform

		protected override IEnumerable<NodeChild> getChildren()
		{
			yield return new NodeChild(_Callback, x => _Callback = x);
		}

		#endregion

		#region Emit

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			if (_Node.Expression != null)
			{
				_Node.Expression.EmitNodeForAccess(ctx);
			}

			_Callback.Emit(ctx, true);

			var method = _IsSubscription ? _Event.AddMethod : _Event.RemoveMethod;
			gen.EmitCall(method, true);
		}

		#endregion
	}
}
