using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A function that has a name.
	/// </summary>
	public class NamedFunctionNode : FunctionNode
	{
		/// <summary>
		/// Function name.
		/// </summary>
		public string Name { get; set; }

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();

			base.Compile(ctx, mustReturn);
		}

		#region Equality members

		protected bool Equals(NamedFunctionNode other)
		{
			return base.Equals(other) && string.Equals(Name, other.Name);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((NamedFunctionNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (base.GetHashCode() * 397) ^ (Name != null ? Name.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
