using Lens.Compiler;

namespace Lens.SyntaxTree.ControlFlow
{
	internal class UsingNode : NodeBase
	{
		/// <summary>
		/// Namespace to be resolved.
		/// </summary>
		public string Namespace { get; set; }

		protected override void compile(Context ctx, bool mustReturn)
		{
			// does nothing
			// all UsingNodes are processed by Context.CreateFromNodes()
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
