using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// An anonymous function definition node.
	/// </summary>
	internal abstract class FunctionNodeBase : NodeBase
	{
		protected FunctionNodeBase()
		{
			Arguments = new List<FunctionArgument>();
		}

		/// <summary>
		/// Function arguments.
		/// </summary>
		public List<FunctionArgument> Arguments { get; set; }

		/// <summary>
		/// Function body.
		/// </summary>
		public CodeBlockNode Body { get; protected set; }

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			return Body.Resolve(ctx);
		}

		public override IEnumerable<NodeChild> GetChildren()
		{
			return Body.GetChildren();
		}

		#region Equality members

		protected bool Equals(FunctionNodeBase other)
		{
			return Arguments.SequenceEqual(other.Arguments) && Equals(Body, other.Body);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((FunctionNodeBase)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Arguments != null ? Arguments.GetHashCode() : 0) * 397) ^ (Body != null ? Body.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
