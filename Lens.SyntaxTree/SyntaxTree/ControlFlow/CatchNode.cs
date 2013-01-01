using System;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The safe block of code.
	/// </summary>
	public class CatchNode : NodeBase, IStartLocationTrackingEntity
	{
		public CatchNode()
		{
			Code = new CodeBlockNode();	
		}

		/// <summary>
		/// The type of the exception this catch block handles.
		/// Null means any exception.
		/// </summary>
		public TypeSignature ExceptionType { get; set; }

		/// <summary>
		/// A variable to assign the exception to.
		/// </summary>
		public string ExceptionVariable { get; set; }

		/// <summary>
		/// The code block.
		/// </summary>
		public CodeBlockNode Code { get; set; }

		public override LexemLocation EndLocation
		{
			get { return Code.EndLocation; }
			set { LocationSetError(); }
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		#region Equality members

		protected bool Equals(CatchNode other)
		{
			return Equals(ExceptionType, other.ExceptionType)
				&& string.Equals(ExceptionVariable, other.ExceptionVariable)
				&& Equals(Code, other.Code);
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
			unchecked
			{
				int hashCode = (ExceptionType != null ? ExceptionType.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (ExceptionVariable != null ? ExceptionVariable.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Code != null ? Code.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion
	}
}
