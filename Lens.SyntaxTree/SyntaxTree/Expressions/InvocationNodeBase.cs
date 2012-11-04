using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A base class for various forms of method invocation that stores arguments.
	/// </summary>
	abstract public class InvocationNodeBase : NodeBase, IStartLocationTrackingEntity
	{
		protected InvocationNodeBase()
		{
			Arguments = new List<NodeBase>();
		}

		/// <summary>
		/// The arguments of the invocation.
		/// </summary>
		public List<NodeBase> Arguments { get; set; }

		public override LexemLocation EndLocation
		{
			// Invocation of a parameterless function still requires a 'unit' argument,
			// so the 'Last()' method shouldn't fail.
			get { return Arguments.Last().EndLocation; }
			set { LocationSetError(); }
		}
		
		#region Equality members

		protected bool Equals(InvocationNodeBase other)
		{
			return Arguments.SequenceEqual(other.Arguments);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((InvocationNodeBase)obj);
		}

		public override int GetHashCode()
		{
			return (Arguments != null ? Arguments.GetHashCode() : 0);
		}

		#endregion
	}
}
