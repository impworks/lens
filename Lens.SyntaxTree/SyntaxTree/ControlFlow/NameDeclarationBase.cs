using System;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A base class for variable and constant declarations.
	/// </summary>
	public class NameDeclarationBase : NodeBase
	{
		/// <summary>
		/// The name of the variable.
		/// </summary>
		public string Name
		{
			get { return VariableInfo.Name; }
			set { VariableInfo.Name = value; }
		}

		/// <summary>
		/// The value to assign to the variable.
		/// </summary>
		public NodeBase Value { get; set; }

		/// <summary>
		/// Variable information.
		/// </summary>
		public VariableInfo VariableInfo { get; protected set; }

		public override LexemLocation EndLocation
		{
			get { return Value.EndLocation; }
			set { LocationSetError(); }
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}

		#region Equality members

		protected bool Equals(NameDeclarationBase other)
		{
			return Equals(Value, other.Value) && Equals(VariableInfo, other.VariableInfo);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((NameDeclarationBase)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Value != null ? Value.GetHashCode() : 0) * 397) ^ (VariableInfo != null ? VariableInfo.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
