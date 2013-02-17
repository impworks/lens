using System;
using System.Linq;
using System.Reflection;

namespace Lens.SyntaxTree.Utils
{
	/// <summary>
	/// A collection of helpful methods for types.
	/// </summary>
	public static class TypeExtensions
	{
		static TypeExtensions()
		{
			SignedIntegerTypes = new[]
			{
				typeof (sbyte),
				typeof (short),
				typeof (int),
				typeof (long),
				typeof (decimal)
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
				typeof (double)
			};
		}

		public static Type[] SignedIntegerTypes { get; private set; }
		public static Type[] UnsignedIntegerTypes { get; private set; }
		public static Type[] FloatTypes { get; private set; }

		/// <summary>
		/// Checks if a type is a <see cref="Nullable{T}"/>.
		/// </summary>
		/// <param name="type">Checked type.</param>
		/// <returns><c>true</c> if type is a <see cref="Nullable{T}"/>.</returns>
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

		/// <summary>
		/// Checks if a type is any of the numeric types.
		/// </summary>
		public static bool IsNumericType(this Type type)
		{
			return type.IsSignedIntegerType()
			       || type.IsFloatType()
			       || type.IsUnsignedIntegerType();
		}

		/// <summary>
		/// Checks if a variable of given type can be assigned from other type (including type extension).
		/// </summary>
		/// <returns></returns>
		public static bool IsExtendablyAssignableFrom(this Type varType, Type exprType)
		{
			return varType.DistanceFrom(exprType) < int.MaxValue;
		}

		/// <summary>
		/// Get the best numeric operation type for two operands.
		/// </summary>
		/// <param name="type1">First operand type.</param>
		/// <param name="type2">Second operand type.</param>
		/// <returns>Operation type. <c>null</c> if operation not permitted.</returns>
		public static Type GetNumericOperationType(Type type1, Type type2)
		{
			if (type1.IsFloatType() || type2.IsFloatType())
			{
				if (type1 == typeof (decimal) || type2 == typeof (decimal))
					return null;
				
				if (type1 == typeof(long) || type2 == typeof(long))
					return typeof (double);

				return MostWideType(FloatTypes, type1, type2);
			}

			if (type1.IsSignedIntegerType() && type2.IsSignedIntegerType())
			{
				var types = SignedIntegerTypes.SkipWhile(type => type != typeof (int)).ToArray();
				return MostWideType(types, type1, type2);
			}

			if (type1.IsUnsignedIntegerType() && type2.IsUnsignedIntegerType())
			{
				var index1 = Array.IndexOf(UnsignedIntegerTypes, type1);
				var index2 = Array.IndexOf(UnsignedIntegerTypes, type2);
				var uintIndex = Array.IndexOf(UnsignedIntegerTypes, typeof (uint));
				if (index1 < uintIndex && index2 < uintIndex)
					return typeof (int);

				return MostWideType(UnsignedIntegerTypes, type1, type2);
			}

			// type1.IsSignedIntegerType() && type2.IsUnsignedIntegerType() or vice versa:
			return null;
		}

		/// <summary>
		/// Gets assignment type distance.
		/// </summary>
		public static int DistanceFrom(this Type varType, Type exprType)
		{
			if (varType == exprType)
				return 0;

			if (varType.IsNullableType() && exprType == Nullable.GetUnderlyingType(varType))
				return 1;

			if (varType.IsNumericType() && exprType.IsNumericType())
				return NumericTypeConversion(varType, exprType);

			if (varType == typeof (object) && exprType.IsValueType)
				return 1;

			if (varType.IsInterface && IsImplementedBy(varType, exprType))
				return 1;

			int result;
			if (IsDerivedFrom(exprType, varType, out result))
				return result;

			if (varType.IsArray && exprType.IsArray)
				return varType.GetElementType().DistanceFrom(exprType.GetElementType());

			if (varType.IsGenericType && exprType.IsGenericType)
				return GenericDistance(varType, exprType);

			return int.MaxValue;
		}

		private static Type MostWideType(Type[] types, Type type1, Type type2)
		{
			var index1 = Array.IndexOf(types, type1);
			var index2 = Array.IndexOf(types, type2);
			var index = Math.Max(index1, index2);
			return types[index < 0 ? 0 : index];
		}

		private static int NumericTypeConversion(Type varType, Type exprType)
		{
			if (varType.IsSignedIntegerType() && exprType.IsSignedIntegerType())
				return SimpleNumericConversion(varType, exprType, SignedIntegerTypes);

			if (varType.IsUnsignedIntegerType() && exprType.IsUnsignedIntegerType())
				return SimpleNumericConversion(varType, exprType, UnsignedIntegerTypes);
			
			if (varType.IsFloatType() && exprType.IsFloatType())
				return SimpleNumericConversion(varType, exprType, FloatTypes);
			
			if (varType.IsSignedIntegerType() && exprType.IsUnsignedIntegerType())
				return UnsignedToSignedConversion(varType, exprType);
			
			if (varType.IsFloatType() && exprType.IsSignedIntegerType())
				return SignedToFloatConversion(varType, exprType);

			if (varType.IsFloatType() && exprType.IsUnsignedIntegerType())
			{
				var correspondingSignedType = GetCorrespondingSignedType(varType);
				var result = UnsignedToSignedConversion(correspondingSignedType, exprType);

				return result == int.MaxValue
					? int.MaxValue
					: result + 1;
			}

			return int.MaxValue;
		}

		private static int SimpleNumericConversion(Type varType, Type exprType, Type[] conversionChain)
		{
			var varTypeIndex = Array.IndexOf(conversionChain, varType);
			var exprTypeIndex = Array.IndexOf(conversionChain, exprType);
			if (varTypeIndex < exprTypeIndex)
				return int.MaxValue;

			return varTypeIndex - exprTypeIndex;
		}

		private static int UnsignedToSignedConversion(Type varType, Type exprType)
		{
			// No unsigned type can be converted to the signed byte.
			if (varType == typeof (sbyte))
				return int.MaxValue;

			var index = Array.IndexOf(SignedIntegerTypes, varType);
			var correspondingUnsignedType = UnsignedIntegerTypes[index - 1]; // only expanding conversions allowed

			var result = SimpleNumericConversion(correspondingUnsignedType, exprType, UnsignedIntegerTypes);
			return result == int.MaxValue
				? int.MaxValue
				: result + 1;
		}

		private static int SignedToFloatConversion(Type varType, Type exprType)
		{
			var targetType = GetCorrespondingSignedType(varType);

			var result = SimpleNumericConversion(targetType, exprType, SignedIntegerTypes);
			return result == int.MaxValue
				? int.MaxValue
				: result + 1;
		}

		private static Type GetCorrespondingSignedType(Type floatType)
		{
			if (floatType == typeof (float))
				return typeof (int);

			if (floatType == typeof (double))
				return typeof (long);

			return null;
		}

		private static bool IsImplementedBy(Type interfaceType, Type implementor)
		{
			return implementor.GetInterfaces().Contains(interfaceType);
		}

		private static bool IsDerivedFrom(Type derivedType, Type baseType, out int distance)
		{
			distance = 0;
			var current = derivedType;
			while (current != null && current != baseType)
			{
				current = current.BaseType;
				++distance;
			}

			return current == baseType;
		}

		private static int GenericDistance(Type varType, Type exprType)
		{
			var definition = varType.GetGenericTypeDefinition();
			if (definition != exprType.GetGenericTypeDefinition())
				return int.MaxValue;

			var arguments = definition.GetGenericArguments();
			var arguments1 = varType.GetGenericArguments();
			var arguments2 = exprType.GetGenericArguments();

			var result = 0;
			for (var i = 0; i < arguments1.Length; ++i)
			{
				var argument1 = arguments1[i];
				var argument2 = arguments2[i];
				if (argument1 == argument2)
					continue;

				var argument = arguments[i];
				var attributes = argument.GenericParameterAttributes;

				int conversionResult;
				if (attributes.HasFlag(GenericParameterAttributes.Contravariant))
				{
					// dist(X<in T1>, X<in T2>) = dist(T2, T1)
					conversionResult = argument2.DistanceFrom(argument1);
				}
				else if (attributes.HasFlag(GenericParameterAttributes.Covariant))
				{
					// dist(X<out T1>, X<out T2>) = dist(T1, T2)
					conversionResult = argument1.DistanceFrom(argument2);
				}
				else
				{
					// No possible conversion found.
					return int.MaxValue;
				}

				if (conversionResult == int.MaxValue)
					return int.MaxValue;

				checked
				{
					result += conversionResult;
				}
			}

			return result;
		}
	}
}
