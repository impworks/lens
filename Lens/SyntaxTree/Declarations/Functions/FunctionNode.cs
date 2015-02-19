using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Attributes;

namespace Lens.SyntaxTree.Declarations.Functions
{
	/// <summary>
	/// A function that has a name.
	/// 
	/// This node is for parser only - it gets translated to a MethodEntity during compilation stage.
	/// </summary>
	internal class FunctionNode : FunctionNodeBase
	{
		#region Constructor

		public FunctionNode()
		{
			Body = new CodeBlockNode(ScopeKind.FunctionRoot);
		}

		#endregion

		#region Fields

		/// <summary>
		/// Function attributes.
		/// </summary>
		public List<AttributeNode> Attributes { get; set; }

		/// <summary>
		/// Function name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Signature of function return type.
		/// </summary>
		public TypeSignature ReturnTypeSignature { get; set; }

		/// <summary>
		/// Checks whether the function can be memoized.
		/// </summary>
		public bool IsPure { get; set; }

		#endregion

		#region Debug

		protected bool Equals(FunctionNode other)
		{
			return base.Equals(other)
			       && string.Equals(Name, other.Name)
			       && IsPure.Equals(other.IsPure)
			       && Equals(ReturnTypeSignature, other.ReturnTypeSignature)
				   && Attributes.SequenceEqual(other.Attributes);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((FunctionNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = base.GetHashCode();
				hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ IsPure.GetHashCode();
				hashCode = (hashCode * 397) ^ (ReturnTypeSignature != null ? ReturnTypeSignature.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion
	}
}
