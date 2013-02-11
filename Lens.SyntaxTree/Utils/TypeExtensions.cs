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
		/// Gets assignment type distance.
		/// </summary>
		public static int DistanceFrom(this Type varType, Type exprType)
		{
			if (varType == exprType)
			{
				return 0;
			}

			if (varType.IsNumericType() && exprType.IsNumericType())
			{
				return NumericTypeConversion(varType, exprType);
			}

			if (varType == typeof (object) && exprType.IsValueType)
			{
				return 1;
			}

			if (varType.IsInterface && IsImplementedBy(varType, exprType))
			{
				return 1;
			}

			int result;
			if (IsDerivedFrom(exprType, varType, out result))
			{
				return result;
			}

			if (varType.IsGenericType && exprType.IsGenericType)
			{
				return GenericDistance(varType, exprType);
			}

			return int.MaxValue;
		}

		private static int NumericTypeConversion(Type varType, Type exprType)
		{
			if (varType.IsSignedIntegerType() && exprType.IsSignedIntegerType())
			{
				return SimpleNumericConversion(varType, exprType, SignedIntegerTypes);
			}
			else if (varType.IsUnsignedIntegerType() && exprType.IsUnsignedIntegerType())
			{
				return SimpleNumericConversion(varType, exprType, UnsignedIntegerTypes);
			}
			else if (varType.IsFloatType() && exprType.IsFloatType())
			{
				return SimpleNumericConversion(varType, exprType, FloatTypes);
			}
			else if (varType.IsSignedIntegerType() && exprType.IsUnsignedIntegerType())
			{
				return UnsignedToSignedConversion(varType, exprType);
			}
			else if (varType.IsFloatType() && exprType.IsSignedIntegerType())
			{
				return SignedToFloatConversion(varType, exprType);
			}
			else if (varType.IsFloatType() && exprType.IsUnsignedIntegerType())
			{
				var correspondingSignedType = GetCorrespondingSignedType(varType);
				int result = UnsignedToSignedConversion(correspondingSignedType, exprType);
				if (result == int.MaxValue)
				{
					return int.MaxValue;
				}

				checked
				{
					return result + 1;
				}
			}

			return int.MaxValue;
		}

		private static int SimpleNumericConversion(Type varType, Type exprType, Type[] conversionChain)
		{
			int varTypeIndex = Array.IndexOf(conversionChain, varType);
			int exprTypeIndex = Array.IndexOf(conversionChain, exprType);
			if (varTypeIndex < exprTypeIndex)
			{
				return int.MaxValue;
			}

			return varTypeIndex - exprTypeIndex;
		}

		private static int UnsignedToSignedConversion(Type varType, Type exprType)
		{
			if (varType == typeof (byte))
			{
				// No unsigned type can be converted to the signed byte.
				return int.MaxValue;
			}

			int index = Array.IndexOf(SignedIntegerTypes, varType);
			var correspondingUnsignedType = UnsignedIntegerTypes[index - 1]; // only expanding conversions allowed

			int result = SimpleNumericConversion(correspondingUnsignedType, exprType, UnsignedIntegerTypes);
			if (result == int.MaxValue)
			{
				return int.MaxValue;
			}

			checked
			{
				return result + 1;
			}
		}

		private static int SignedToFloatConversion(Type varType, Type exprType)
		{
			var targetType = GetCorrespondingSignedType(varType);

			int result = SimpleNumericConversion(targetType, exprType, SignedIntegerTypes);
			if (result == int.MaxValue)
			{
				return int.MaxValue;
			}

			checked
			{
				return result + 1;
			}
		}

		private static Type GetCorrespondingSignedType(Type floatType)
		{
			if (floatType == typeof (float))
			{
				return typeof (int);
			}
			else if (floatType == typeof (double))
			{
				return typeof (long);
			}

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
			{
				return int.MaxValue;
			}

			var arguments = definition.GetGenericArguments();
			var arguments1 = varType.GetGenericArguments();
			var arguments2 = exprType.GetGenericArguments();

			int result = 0;
			for (int i = 0; i < arguments1.Length; ++i)
			{
				var argument1 = arguments1[i];
				var argument2 = arguments2[i];
				if (argument1 == argument2)
				{
					continue;
				}

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
				{
					return int.MaxValue;
				}

				checked
				{
					result += conversionResult;
				}
			}

			return result;
		}
	}
}
