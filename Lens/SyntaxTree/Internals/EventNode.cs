using System;
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
			_Node = node;
			_IsSubscription = isSubscription;
		}

		#endregion

		#region Fields

		/// <summary>
		/// Node containing the event expression and name.
		/// </summary>
		private readonly SetMemberNode _Node;

		/// <summary>
		/// Flag indicating that the 
		/// </summary>
		private readonly bool _IsSubscription;

		/// <summary>
		/// The event entity.
		/// </summary>
		private readonly EventWrapper _Event;

		#endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			_Node.Value.Resolve(ctx);

			return base.resolve(ctx, mustReturn);
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

			_Node.Value.Emit(ctx, true);

			var method = _IsSubscription ? _Event.AddMethod : _Event.RemoveMethod;
			gen.EmitCall(method, true);
		}

		#endregion
	}
}
