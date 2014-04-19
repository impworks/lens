using Lens.Compiler;

namespace Lens.SyntaxTree.ControlFlow
{
	internal class UseNode : NodeBase
	{
		/// <summary>
		/// Namespace to be resolved.
		/// </summary>
		public string Namespace { get; set; }

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			// does nothing
			// all UseNodes are processed by Context.CreateFromNodes()
		}

		#region Equality members

		protected bool Equals(UseNode other)
		{
			return string.Equals(Namespace, other.Namespace);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((UseNode)obj);
		}

		public override int GetHashCode()
		{
			return (Namespace != null ? Namespace.GetHashCode() : 0);
		}

		#endregion

		public override string ToString()
		{
			return string.Format("use({0})", Namespace);
		}
	}
}
