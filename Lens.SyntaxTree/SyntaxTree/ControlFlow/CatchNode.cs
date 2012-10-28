using System;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The safe block of code.
	/// </summary>
	public class CatchNode : NodeBase
	{
		/// <summary>
		/// The code block.
		/// </summary>
		public CodeBlockNode Code { get; set; }

		public override Utils.LexemLocation EndLocation
		{
			get { return Code.EndLocation; }
			set { LocationSetError(); }
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}

		#region Equality members

		protected bool Equals(CatchNode other)
		{
			return Equals(Code, other.Code);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((CatchNode)obj);
		}

		public override int GetHashCode()
		{
			return (Code != null ? Code.GetHashCode() : 0);
		}

		#endregion
	}
}
