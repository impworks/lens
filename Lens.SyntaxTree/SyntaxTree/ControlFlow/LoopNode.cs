using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	public class LoopNode : NodeBase, IStartLocationTrackingEntity
	{
		public LoopNode()
		{
			Body = new CodeBlockNode();	
		}

		/// <summary>
		/// The condition.
		/// </summary>
		public NodeBase Condition { get; set; }

		/// <summary>
		/// The body of the loop.
		/// </summary>
		public CodeBlockNode Body { get; set; }

		public override LexemLocation EndLocation
		{
			get { return Body.EndLocation; }
			set { LocationSetError(); }
		}

		protected override Type resolveExpressionType(Context ctx)
		{
			return Body.GetExpressionType(ctx);
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Condition;
			yield return Body;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		#region Equality members

		protected bool Equals(LoopNode other)
		{
			return Equals(Condition, other.Condition) && Equals(Body, other.Body);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((LoopNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Condition != null ? Condition.GetHashCode() : 0) * 397) ^ (Body != null ? Body.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
