using Lens.Compiler;

namespace Lens.SyntaxTree.Literals
{
	/// <summary>
	/// A node representing a unit literal ().
	/// </summary>
	internal class UnitNode : NodeBase
	{
		protected override void emitCode(Context ctx, bool mustReturn)
		{
			// does nothing
		}

		#region Equality members

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			return obj.GetType() == GetType();
		}

		public override int GetHashCode()
		{
			return 0;
		}

		#endregion

		public override string ToString()
		{
			return "()";
		}
	}
}
