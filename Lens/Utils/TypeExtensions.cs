using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.Compiler;
using Lens.SyntaxTree.Literals;

namespace Lens.Utils
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
				typeof (double)
			};

			m_DistanceCache = new Dictionary<Tuple<Type, Type, bool>, int>();
		}

		public static Type[] SignedIntegerTypes { get; private set; }
		public static Type[] UnsignedIntegerTypes { get; private set; }
		public static Type[] FloatTypes { get; private set; }

		private static Dictionary<Tuple<Type, Type, bool>, int> m_DistanceCache;

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

		public static bool IsIntegerType(this Type type)
		{
			return type.IsSignedIntegerType() || type.IsUnsignedIntegerType();
		}

		/// <summary>
		/// Checks if a type is any of the numeric types.
		/// </summary>
		public static bool IsNumericType(this Type type)
		{
			return type.IsIntegerType()
			       || type.IsFloatType();
		}

		/// <summary>
		/// Checks if the type is returnable.
		/// </summary>
		public static bool IsNotVoid(this Type type)
		{
			return type != typeof (void) && type != typeof (Unit);
		}

		/// <summary>
		/// Checks if the type is void.
		/// </summary>
		public static bool IsVoid(this Type type)
		{
			return type == typeof(void) || type == typeof(Unit);
		}

		/// <summary>
		/// Checks if the type is a struct.
		/// </summary>
		public static bool IsStruct(this Type type)
		{
			return type.IsValueType && !type.IsNumericType();
		}

		/// <summary>
		/// Checks if a variable of given type can be assigned from other type (including type extension).
		/// </summary>
		/// <param name="varType">Type of assignment target (ex. variable)</param>
		/// <param name="exprType">Type of assignment source (ex. expression)</param>
		/// <param name="exactly">Checks whether types must be compatible as-is, or additional code may be implicitly issued by the compiler.</param>
		/// <returns></returns>
		public static bool IsExtendablyAssignableFrom(this Type varType, Type exprType, bool exactly = false)
		{
			return varType.DistanceFrom(exprType, exactly) < int.MaxValue;
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
		/// Gets the most common type that all the given types would fit into.
		/// </summary>
		public static Type GetMostCommonType(this IEnumerable<Type> types)
		{
			var arr = types.ToArray();

			if (arr.Length == 0)
				return null;

			if (arr.Length == 1)
				return arr[0];

			// try to get the most wide type
			Type curr = null;
			foreach (var type in arr)
			{
				if (type.IsInterface)
				{
					curr = typeof (object);
					break;
				}

				curr = getMostCommonType(curr, type);
				if (curr == typeof (object))
					break;
			}

			// check for cases that are not transitively castable
			// for example: new [1; 1.2; null]
			// int -> double is fine, double -> Nullable<double> is fine as well
			// but int -> Nullable<double> is currently forbidden
			foreach (var type in arr)
			{
				if (!curr.IsExtendablyAssignableFrom(type))
				{
					curr = typeof (object);
					break;
				}
			}

			if (!curr.IsAnyOf(typeof (object), typeof (ValueType), typeof (Delegate), typeof (Enum)))
				return curr;

			// try to get common interfaces
			var ifaces = arr[0].GetInterfaces().AsEnumerable();
			for (var idx = 1; idx < arr.Length; idx++)
			{
				ifaces = ifaces.Intersect(arr[idx].IsInterface ? new [] { arr[idx] } : arr[idx].GetInterfaces());
				if (!ifaces.Any())
					break;
			}

			var iface = getMostSpecificInterface(ifaces);
			return iface ?? typeof (object);
		}

		/// <summary>
		/// Gets the most common type between two.
		/// </summary>
		private static Type getMostCommonType(Type left, Type right)
		{
			// corner case
			if (left == null || left == right)
				return right;

			if (right.IsInterface)
				return typeof (object);

			// valuetype & null
			if (left == typeof (NullType) && right.IsValueType)
				return typeof (Nullable<>).MakeGenericType(right);

			if (right == typeof(NullType) && left.IsValueType)
				return typeof(Nullable<>).MakeGenericType(left);

			// valuetype & Nullable<valuetype>
			if (left.IsNullableType() && left.GetGenericArguments()[0] == right)
				return left;

			if (right.IsNullableType() && right.GetGenericArguments()[0] == left)
				return right;

			// numeric extensions
			if (left.IsNumericType() && right.IsNumericType())
				return GetNumericOperationType(left, right) ?? typeof(object);

			// arrays
			if (left.IsArray && right.IsArray)
			{
				var leftElem = left.GetElementType();
				var rightElem = right.GetElementType();
				return leftElem.IsValueType || rightElem.IsValueType
					? typeof (object)
					: getMostCommonType(leftElem, rightElem).MakeArrayType();
			}

			// inheritance
			var currLeft = left;
			while (currLeft != null)
			{
				var currRight = right;
				while (currRight != null)
				{
					if (currLeft == currRight)
						return currLeft;

					currRight = currRight.BaseType;
				}

				currLeft = currLeft.BaseType;
			}

			return typeof(object);
		}

		private static Type getMostSpecificInterface(IEnumerable<Type> ifaces)
		{
			var remaining = ifaces.ToDictionary(i => i, i => true);
			foreach (var iface in ifaces)
			{
				foreach (var curr in iface.GetInterfaces())
					remaining.Remove(curr);
			}

			if (remaining.Count == 1)
				return remaining.First().Key;

			var preferred = new[] {typeof (IList<>), typeof (IEnumerable<>), typeof (IList)};
			foreach (var pref in preferred)
			{
				foreach (var curr in remaining.Keys)
					if (curr == pref || (curr.IsGenericType && curr.GetGenericTypeDefinition() == pref))
						return curr;
			}

			return null;
		}

		/// <summary>
		/// Gets distance between two types.
		/// This method is memoized.
		/// </summary>
		public static int DistanceFrom(this Type varType, Type exprType, bool exactly = false)
		{
			var key = new Tuple<Type, Type, bool>(varType, exprType, exactly);

			if (!m_DistanceCache.ContainsKey(key))
				m_DistanceCache.Add(key, distanceFrom(varType, exprType, exactly));

			return m_DistanceCache[key];
		}

		
		private static int distanceFrom(Type varType, Type exprType, bool exactly = false)
		{
			if (varType == exprType)
				return 0;

			if (varType.IsByRef)
				return varType.GetElementType() == exprType ? 0 : int.MaxValue;

			if (!exactly)
			{
				if (varType.IsNullableType() && exprType == Nullable.GetUnderlyingType(varType))
					return 1;

				if ((varType.IsClass || varType.IsNullableType()) && exprType == typeof (NullType))
					return 1;

				if (varType.IsNumericType() && exprType.IsNumericType())
					return NumericTypeConversion(varType, exprType);
			}

			if (varType == typeof (object))
			{
				if (exprType.IsValueType)
					return exactly ? int.MaxValue : 1;

				if (exprType.IsInterface)
					return 1;
			}

			if (varType.IsInterface)
			{
				if (exprType.IsInterface)
					return InterfaceDistance(varType, new[] { exprType }.Union(GenericHelper.GetInterfaces(exprType)));

				// casting expression to interface takes 1 step
				var dist = InterfaceDistance(varType, GenericHelper.GetInterfaces(exprType));
				if (dist < int.MaxValue)
					return dist + 1;
			}

			if (varType.IsGenericParameter || exprType.IsGenericParameter)
				return GenericParameterDistance(varType, exprType);

			if (varType.IsGenericType && exprType.IsGenericType)
				return GenericDistance(varType, exprType);

			int result;
			if (IsDerivedFrom(exprType, varType, out result))
				return result;

			if (varType.IsArray && exprType.IsArray)
			{
				var varElType = varType.GetElementType();
				var exprElType = exprType.GetElementType();

				var areRefs = !varElType.IsValueType && !exprElType.IsValueType;
				var generic = varElType.IsGenericParameter || exprElType.IsGenericParameter;
				if(areRefs || generic)
					return varElType.DistanceFrom(exprElType, exactly);
			}

			return int.MaxValue;
		}

		private static int InterfaceDistance(Type interfaceType, IEnumerable<Type> ifaces, bool exactly = false)
		{
			var min = int.MaxValue;
			foreach (var iface in ifaces)
			{
				if (iface == interfaceType)
					return 0;

				if (interfaceType.IsGenericType && iface.IsGenericType)
				{
					var dist = GenericDistance(interfaceType, iface, exactly);
					if (dist < min)
						min = dist;
				}
			}

			return min;
		}

		private static int GenericParameterDistance(Type varType, Type exprType, bool exactly = false)
		{
			// generic parameter is on the same level of inheritance as the expression
			// therefore getting its parent type does not take a step
			return varType.IsGenericParameter
				? DistanceFrom(varType.BaseType, exprType, exactly)
				: DistanceFrom(exprType.BaseType, varType, exactly);
		}

		private static bool IsImplicitCastable(Type varType, Type exprType)
		{
			if (exprType is TypeBuilder)
				return false;

			try
			{
				return exprType.GetMethods().Any(m => m.Name == "op_Implicit" && m.ReturnType == varType);
			}
			catch(NotSupportedException)
			{
				return false;
			}
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
			// no unsigned type can be converted to the signed byte.
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

		private static int GenericDistance(Type varType, Type exprType, bool exactly = false)
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
				if (argument1.IsGenericParameter)
				{
					// generic parameter may be substituted with anything
					// including value types
					conversionResult = GenericParameterDistance(argument1, argument2, exactly);
				}
				else if (argument2.IsGenericParameter)
				{
					conversionResult = GenericParameterDistance(argument2, argument1, exactly);
				}
				else if (attributes.HasFlag(GenericParameterAttributes.Contravariant))
				{
					// generic variance applies to ref-types only
					if (argument1.IsValueType)
						return int.MaxValue;

					// dist(X<in T1>, X<in T2>) = dist(T2, T1)
					conversionResult = argument2.DistanceFrom(argument1, exactly);
				}
				else if (attributes.HasFlag(GenericParameterAttributes.Covariant))
				{
					if (argument2.IsValueType)
						return int.MaxValue;

					// dist(X<out T1>, X<out T2>) = dist(T1, T2)
					conversionResult = argument1.DistanceFrom(argument2, exactly);
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

		/// <summary>
		/// Checks if a type implements an interface.
		/// </summary>
		/// <param name="type">Type to check.</param>
		/// <param name="iface">Desired interface.</param>
		/// <param name="unwindGenerics">A flag indicating that generic arguments should be discarded from both the type and the interface.</param>
		public static bool Implements(this Type type, Type iface, bool unwindGenerics)
		{
			if (unwindGenerics && type.IsGenericType && iface.IsGenericType)
			{
				type = type.GetGenericTypeDefinition();
				iface = iface.GetGenericTypeDefinition();
			}

			return type.GetInterfaces().Contains(iface);
		}
	}
}
