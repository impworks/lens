using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The try node.
	/// </summary>
	public class TryNode : NodeBase
	{
		public TryNode()
		{
			CatchClauses = new List<CatchNode>();
		}

		/// <summary>
		/// The protected code.
		/// </summary>
		public CodeBlockNode Code { get; set; }

		/// <summary>
		/// The list of catch clauses.
		/// </summary>
		public List<CatchNode> CatchClauses { get; private set; }

		public override Utils.LexemLocation EndLocation
		{
			get { return CatchClauses.Last().EndLocation; }
			set { throw new InvalidOperationException("Try node's end location cannot be set manually!"); }
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}

		#region Equality members

		protected bool Equals(TryNode other)
		{
			return Equals(Code, other.Code) && CatchClauses.DeepEquals(other.CatchClauses);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((TryNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Code != null ? Code.GetHashCode() : 0) * 397) ^ (CatchClauses != null ? CatchClauses.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
