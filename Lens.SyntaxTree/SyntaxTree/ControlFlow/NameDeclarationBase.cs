using System;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A base class for variable and constant declarations.
	/// </summary>
	public class NameDeclarationBase : NodeBase
	{
		public NameDeclarationBase()
		{
			NameInfo = new LexicalNameInfo();
		}

		/// <summary>
		/// The name of the variable.
		/// </summary>
		public string Name
		{
			get { return NameInfo.Name; }
			set { NameInfo.Name = value; }
		}

		/// <summary>
		/// The value to assign to the variable.
		/// </summary>
		public NodeBase Value { get; set; }

		/// <summary>
		/// Variable information.
		/// </summary>
		public LexicalNameInfo NameInfo { get; protected set; }

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
			return Equals(Value, other.Value) && Equals(NameInfo, other.NameInfo);
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
				return ((Value != null ? Value.GetHashCode() : 0) * 397) ^ (NameInfo != null ? NameInfo.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
