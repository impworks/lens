using System;
using System.Reflection.Emit;
using Lens.SyntaxTree;

namespace Lens.Compiler
{
	/// <summary>
	/// A node representing a function argument definition.
	/// </summary>
	internal class FunctionArgument : LocationEntity
	{
		public FunctionArgument()
		{ }

		public FunctionArgument(string name, Type type, bool isRefArg = false)
		{
			Name = name;
			Type = type;
			IsRefArgument = isRefArg;
		}

		public FunctionArgument(string name, TypeSignature type, bool isRefArg = false)
		{
			Name = name;
			TypeSignature = type;
			IsRefArgument = isRefArg;
		}

		/// <summary>
		/// Argument name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Argument resolved type.
		/// </summary>
		public Type Type { get; set; }

		/// <summary>
		/// Argument type
		/// </summary>
		public TypeSignature TypeSignature { get; set; }

		/// <summary>
		/// Is the argument passed by reference?
		/// </summary>
		public bool IsRefArgument { get; set; }

		/// <summary>
		/// Parameter builder.
		/// </summary>
		public ParameterBuilder ParameterBuilder { get; set; }

		/// <summary>
		/// Calculates argument type.
		/// </summary>
		public Type GetArgumentType(Context ctx)
		{
			if (Type != null)
				return Type;

			var type = ctx.ResolveType(TypeSignature);
			if (IsRefArgument)
				type = type.MakeByRefType();

			return type;
		}

		#region Equality members

		protected bool Equals(FunctionArgument other)
		{
			return string.Equals(Name, other.Name) && string.Equals(TypeSignature, other.TypeSignature);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((FunctionArgument)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = (Name != null ? Name.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Type != null ? Type.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (TypeSignature != null ? TypeSignature.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion
	}
}
