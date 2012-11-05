using System;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// A node to represent the null literal.
	/// </summary>
	public class NullNode : NodeBase, IStartLocationTrackingEntity, IEndLocationTrackingEntity
	{
		public override Type GetExpressionType()
		{
			return typeof (NullType);
		}

		public override void Compile()
		{
			throw new NotImplementedException();
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
			return "(null)";
		}
	}

	/// <summary>
	/// A pseudotype to represent the null variable.
	/// </summary>
	public class NullType { }
}
