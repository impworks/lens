﻿using System.Collections.Generic;
using System.Linq;

namespace Lens.SyntaxTree.Expressions
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
			get { return Arguments.Any() ? Arguments.Last().EndLocation : StartLocation; }
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