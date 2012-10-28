using System;
using System.Reflection;

namespace Lens.SyntaxTree.Utils
{
	/// <summary>
	/// A cache-friendly version of type signature.
	/// </summary>
	public class TypeSignature
	{
		public TypeSignature(string signature)
		{
			Signature = signature;
		}

		private bool m_IsResolved;
		private Type m_Type;

		public string Signature { get; private set; }

		/// <summary>
		/// The compiled type.
		/// </summary>
		public Type Type
		{
			get
			{
				if (!m_IsResolved)
				{
					m_Type = resolveType();
					m_IsResolved = true;
				}

				return m_Type;
			}
		}

		/// <summary>
		/// Resolves the string version of type into a Type instance.
		/// </summary>
		private Type resolveType()
		{
			// todo: enable namespaces support
			// todo: check in all loaded assemblies
			var types = Assembly.GetExecutingAssembly().GetTypes();
			foreach (var curr in types)
				if (curr.Name == Signature)
					return curr;

			return null;
		}

		#region Equality members

		protected bool Equals(TypeSignature other)
		{
			return string.Equals(Signature, other.Signature);
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
			return (Signature != null ? Signature.GetHashCode() : 0);
		}

		#endregion
	}
}
