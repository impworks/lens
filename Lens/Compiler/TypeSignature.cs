using System.Text;
using Lens.SyntaxTree;

namespace Lens.Compiler
{
	/// <summary>
	/// A cache-friendly version of type signature.
	/// </summary>
	public class TypeSignature : LocationEntity, IStartLocationTrackingEntity, IEndLocationTrackingEntity
	{
		public TypeSignature(string name, params TypeSignature[] args)
		{
			Name = name;
			Arguments = args.Length > 0 ? args : null;
			FullSignature = getSignature(name, args);
		}

		#region Fields

		public readonly string Name;
		public readonly TypeSignature[] Arguments;

		public readonly string FullSignature;

		#endregion

		#region Methods

		private string getSignature(string name, TypeSignature[] args)
		{
			if (args.Length == 0)
				return name;

			var sb = new StringBuilder(name);
			sb.Append("<");

			var idx = 0;
			foreach (var curr in args)
			{
				if (idx > 0)
					sb.Append(", ");

				sb.Append(curr.FullSignature);

				idx++;
			}

			sb.Append(">");

			return sb.ToString();
		}

		/// <summary>
		/// Initializes a type signature with it's string representation.
		/// </summary>
		public static implicit operator TypeSignature(string type)
		{
			return new TypeSignature(type);
		}

		#endregion

		#region Equality members

		protected bool Equals(TypeSignature other)
		{
			return string.Equals(FullSignature, other.FullSignature);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((TypeSignature)obj);
		}

		public override int GetHashCode()
		{
			return FullSignature.GetHashCode();
		}

		#endregion
	}
}
