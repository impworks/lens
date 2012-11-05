using System;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	public class UsingNode : NodeBase, IStartLocationTrackingEntity, IEndLocationTrackingEntity
	{
		/// <summary>
		/// Namespace to be resolved.
		/// </summary>
		public string Namespace { get; set; }

		public override void Compile()
		{
			throw new NotImplementedException();
		}

		#region Equality members

		protected bool Equals(UsingNode other)
		{
			return string.Equals(Namespace, other.Namespace);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((UsingNode)obj);
		}

		public override int GetHashCode()
		{
			return (Namespace != null ? Namespace.GetHashCode() : 0);
		}

		#endregion

		public override string ToString()
		{
			return string.Format("using({0})", Namespace);
		}
	}
}
