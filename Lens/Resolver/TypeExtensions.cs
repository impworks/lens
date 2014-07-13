using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Compiler;
using Lens.Utils;

namespace Lens.Resolver
{
	/// <summary>
	/// A collection of helpful methods for types.
	/// </summary>
	internal static class TypeExtensions
	{
		static TypeExtensions()
		{
			SignedIntegerTypes = new[]
			{
				typeof (sbyte),
				typeof (short),
				typeof (int),
				typeof (long)
			};

			UnsignedIntegerTypes = new[]
			{
				typeof (byte),
				typeof (ushort),
				typeof (uint),
				typeof (ulong),
			};

			FloatTypes = new[]
			{
				typeof (float),
				typeof (double),
				typeof (decimal)
			};
		}

		public static Type[] SignedIntegerTypes { get; private set; }
		public static Type[] UnsignedIntegerTypes { get; private set; }
		public static Type[] FloatTypes { get; private set; }

		#region Type class checking

		/// <summary>
		/// Checks if a type is a <see cref="Nullable{T}"/>.
		/// </summary>
		public static bool IsNullableType(this Type type)
		{
			return type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Nullable<>);
		}

		/// <summary>
		/// Checks if a type is a signed integer type.
		/// </summary>
		public static bool IsSignedIntegerType(this Type type)
		{
			return SignedIntegerTypes.Contains(type);
		}

		/// <summary>
		/// Checks if a type is an unsigned integer type.
		/// </summary>
		public static bool IsUnsignedIntegerType(this Type type)
		{
			return UnsignedIntegerTypes.Contains(type);
		}

		/// <summary>
		/// Checks if a type is a floating point type.
		/// </summary>
		public static bool IsFloatType(this Type type)
		{
			return FloatTypes.Contains(type);
		}

		public static bool IsIntegerType(this Type type)
		{
			return type.IsSignedIntegerType() || type.IsUnsignedIntegerType();
		}

		/// <summary>
		/// Checks if a type is any of the numeric types.
		/// </summary>
		public static bool IsNumericType(this Type type, bool allowNonPrimitives = false)
		{
			if (!allowNonPrimitives && type == typeof (decimal))
				return false;

			return type.IsSignedIntegerType() || type.IsUnsignedIntegerType() || type.IsFloatType();
		}

		/// <summary>
		/// Checks if the type is void.
		/// </summary>
		public static bool IsVoid(this Type type)
		{
			return type == typeof(void) || type == typeof(UnitType);
		}

		/// <summary>
		/// Checks if the type is a struct.
		/// </summary>
		public static bool IsStruct(this Type type)
		{
			return type.IsValueType && !type.IsNumericType();
		}

		#endregion
	}
}
