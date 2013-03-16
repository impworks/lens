using System;
using System.Reflection.Emit;
using Lens.SyntaxTree.SyntaxTree;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// A node representing a function argument definition.
	/// </summary>
	public class FunctionArgument : LocationEntity, IStartLocationTrackingEntity, IEndLocationTrackingEntity
	{
		public FunctionArgument()
		{ }

		public FunctionArgument(string name, Type type, ArgumentModifier modifier = ArgumentModifier.In)
		{
			Name = name;
			Modifier = modifier;
			Type = type;
		}

		public FunctionArgument(string name, TypeSignature type, ArgumentModifier modifier = ArgumentModifier.In)
		{
			Name = name;
			TypeSignature = type;
			Modifier = modifier;
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
		/// Argument modifier
		/// </summary>
		public ArgumentModifier Modifier { get; set; }

		/// <summary>
		/// Parameter builder.
		/// </summary>
		public ParameterBuilder ParameterBuilder { get; set; }

		#region Equality members

		protected bool Equals(FunctionArgument other)
		{
			return string.Equals(Name, other.Name) && string.Equals(TypeSignature, other.TypeSignature) && Modifier == other.Modifier;
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
				hashCode = (hashCode * 397) ^ (int)Modifier;
				return hashCode;
			}
		}

		#endregion
	}

	/// <summary>
	/// Argument type
	/// </summary>
	public enum ArgumentModifier
	{
		In,
		Ref,
		Out
	}
}
