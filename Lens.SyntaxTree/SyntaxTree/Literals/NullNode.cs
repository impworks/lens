using System;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Literals
{
	/// <summary>
	/// A node to represent the null literal.
	/// </summary>
	public class NullNode : NodeBase, IStartLocationTrackingEntity, IEndLocationTrackingEntity
	{
		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return typeof (NullType);
		}

		protected override void compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			gen.EmitNull();
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
