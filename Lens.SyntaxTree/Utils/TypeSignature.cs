using System;
using System.Reflection;
using Lens.SyntaxTree.SyntaxTree;

namespace Lens.SyntaxTree.Utils
{
	/// <summary>
	/// A cache-friendly version of type signature.
	/// </summary>
	public class TypeSignature : LocationEntity, IStartLocationTrackingEntity, IEndLocationTrackingEntity
	{
		public TypeSignature(string signature)
		{
			Signature = signature;
		}

		#region Fields
		
		private bool m_IsResolved;
		private Type m_Type;

		/// <summary>
		/// The signature of the type.
		/// </summary>
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

		#endregion

		#region Methods

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
