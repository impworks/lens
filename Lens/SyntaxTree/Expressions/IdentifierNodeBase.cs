using Lens.Compiler;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// The base node for identifier getters and setters.
	/// </summary>
	internal abstract class IdentifierNodeBase : NodeBase
	{
		#region Fields

		/// <summary>
		/// Identifier name.
		/// </summary>
		public string Identifier { get; set; }

		/// <summary>
		/// Reference to local variable.
		/// </summary>
		public Local Local { get; set; }

		#endregion

		#region Process closures

		public override void ProcessClosures(Context ctx)
		{
			base.ProcessClosures(ctx);

			ctx.Scope.ReferenceLocal(ctx, Identifier ?? Local.Name);
		}

		#endregion

		#region Equality members

		protected bool Equals(IdentifierNodeBase other)
		{
			return string.Equals(Identifier, other.Identifier);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((IdentifierNodeBase)obj);
		}

		public override int GetHashCode()
		{
			return (Identifier != null ? Identifier.GetHashCode() : 0);
		}

		#endregion
	}
}
