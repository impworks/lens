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
		#region Static constructor

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

			m_DistanceCache = new Dictionary<Tuple<Type, Type, bool>, int>();
		}

		#endregion

		#region Fields

		public static readonly Type[] SignedIntegerTypes;
		public static readonly Type[] UnsignedIntegerTypes;
		public static readonly Type[] FloatTypes;

		private static readonly Dictionary<Tuple<Type, Type, bool>, int> m_DistanceCache;

		#endregion

		#region Type class checking

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
		/// Checks if a type is any of integer types, signed or unsigned.
		/// </summary>
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

		#region Type distance

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

		/// <summary>
		/// Calculates the distance between two types.
		/// </summary>
		private static int distanceFrom(Type varType, Type exprType, bool exactly = false)
		{
			if (varType == exprType)
				return 0;

			// partial application
			if (exprType == typeof(UnspecifiedType))
				return 0;

			if (varType.IsByRef)
				return varType.GetElementType() == exprType ? 0 : int.MaxValue;

			if (!exactly)
			{
				if (varType.IsNullableType() && exprType == Nullable.GetUnderlyingType(varType))
					return 1;

				if ((varType.IsClass || varType.IsNullableType()) && exprType == typeof(NullType))
					return 1;

				if (varType.IsNumericType(true) && exprType.IsNumericType(true))
					return numericTypeConversion(varType, exprType);
			}

			if (varType == typeof(object))
			{
				if (exprType.IsValueType)
					return exactly ? int.MaxValue : 1;

				if (exprType.IsInterface)
					return 1;
			}

			if (varType.IsInterface)
			{
				if (exprType.IsInterface)
					return interfaceDistance(varType, new[] { exprType }.Union(exprType.ResolveInterfaces()));

				// casting expression to interface takes 1 step
				var dist = interfaceDistance(varType, exprType.ResolveInterfaces());
				if (dist < int.MaxValue)
					return dist + 1;
			}

			if (varType.IsGenericParameter || exprType.IsGenericParameter)
				return genericParameterDistance(varType, exprType);

			if (exprType.IsLambdaType())
				return lambdaDistance(varType, exprType);

			if (varType.IsGenericType && exprType.IsGenericType)
				return genericDistance(varType, exprType);

			int result;
			if (isDerivedFrom(exprType, varType, out result))
				return result;

			if (varType.IsArray && exprType.IsArray)
			{
				var varElType = varType.GetElementType();
				var exprElType = exprType.GetElementType();

				var areRefs = !varElType.IsValueType && !exprElType.IsValueType;
				var generic = varElType.IsGenericParameter || exprElType.IsGenericParameter;
				if (areRefs || generic)
					return varElType.DistanceFrom(exprElType, exactly);
			}

			return int.MaxValue;
		}

		/// <summary>
		/// Calculates the distance to any of given interfaces.
		/// </summary>
		private static int interfaceDistance(Type interfaceType, IEnumerable<Type> ifaces, bool exactly = false)
		{
			var min = int.MaxValue;
			foreach (var iface in ifaces)
			{
				if (iface == interfaceType)
					return 0;

				if (interfaceType.IsGenericType && iface.IsGenericType)
				{
					var dist = genericDistance(interfaceType, iface, exactly);
					if (dist < min)
						min = dist;
				}
			}

			return min;
		}

		/// <summary>
		/// Checks if a type is a child for some other type.
		/// </summary>
		private static bool isDerivedFrom(Type derivedType, Type baseType, out int distance)
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

		/// <summary>
		/// Calculates compound distance of two generic types' arguments if applicable.
		/// </summary>
		private static int genericDistance(Type varType, Type exprType, bool exactly = false)
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
					conversionResult = genericParameterDistance(argument1, argument2, exactly);
				}
				else if (argument2.IsGenericParameter)
				{
					conversionResult = genericParameterDistance(argument2, argument1, exactly);
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
		/// Checks if a type can be used as a substitute for a generic parameter.
		/// </summary>
		private static int genericParameterDistance(Type varType, Type exprType, bool exactly = false)
		{
			// generic parameter is on the same level of inheritance as the expression
			// therefore getting its parent type does not take a step
			return varType.IsGenericParameter
				? DistanceFrom(varType.BaseType, exprType, exactly)
				: DistanceFrom(exprType.BaseType, varType, exactly);
		}

		/// <summary>
		/// Checks if a lambda signature matches a delegate.
		/// </summary>
		private static int lambdaDistance(Type varType, Type exprType)
		{
			if (!varType.IsCallableType())
				return int.MaxValue;

			var varWrapper = ReflectionHelper.WrapDelegate(varType);
			var exprWrapper = ReflectionHelper.WrapDelegate(exprType);

			if (varWrapper.ArgumentTypes.Length != exprWrapper.ArgumentTypes.Length)
				return int.MaxValue;

			// return type is not checked until lambda argument types are substituted

			var sum = 0;
			for (var idx = 0; idx < varWrapper.ArgumentTypes.Length; idx++)
			{
				var currVar = varWrapper.ArgumentTypes[idx];
				var currExpr = exprWrapper.ArgumentTypes[idx];

				var dist = currVar.DistanceFrom(currExpr);
				if (dist == int.MaxValue)
					return int.MaxValue;

				sum += dist;
			}

			return sum;
		}

		#endregion

		#region Most common type

		/// <summary>
		/// Gets the most common type that all the given types would fit into.
		/// </summary>
		public static Type GetMostCommonType(this Type[] types)
		{
			if (types.Length == 0)
				return null;

			if (types.Length == 1)
				return types[0];

			// try to get the most wide type
			Type curr = null;
			foreach (var type in types)
			{
				if (type.IsInterface)
				{
					curr = typeof(object);
					break;
				}

				curr = getMostCommonType(curr, type);
				if (curr == typeof(object))
					break;
			}

			// check for cases that are not transitively castable
			// for example: new [1; 1.2; null]
			// int -> double is fine, double -> Nullable<double> is fine as well
			// but int -> Nullable<double> is currently forbidden
			foreach (var type in types)
			{
				if (!curr.IsExtendablyAssignableFrom(type))
				{
					curr = typeof(object);
					break;
				}
			}

			if (!curr.IsAnyOf(typeof(object), typeof(ValueType), typeof(Delegate), typeof(Enum)))
				return curr;

			// try to get common interfaces
			var ifaces = types[0].GetInterfaces().AsEnumerable();
			for (var idx = 1; idx < types.Length; idx++)
			{
				ifaces = ifaces.Intersect(types[idx].IsInterface ? new[] { types[idx] } : types[idx].GetInterfaces());
				if (!ifaces.Any())
					break;
			}

			var iface = getMostSpecificInterface(ifaces);
			return iface ?? typeof(object);
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
				return typeof(object);

			// valuetype & null
			if (left == typeof(NullType) && right.IsValueType)
				return typeof(Nullable<>).MakeGenericType(right);

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
					? typeof(object)
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

			var preferred = new[] { typeof(IList<>), typeof(IEnumerable<>), typeof(IList) };
			foreach (var pref in preferred)
			{
				foreach (var curr in remaining.Keys)
					if (curr == pref || (curr.IsGenericType && curr.GetGenericTypeDefinition() == pref))
						return curr;
			}

			return null;
		}

		#endregion

		#region Numeric type conversions

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

				return widestNumericType(FloatTypes, type1, type2);
			}

			if (type1.IsSignedIntegerType() && type2.IsSignedIntegerType())
			{
				var types = SignedIntegerTypes.SkipWhile(type => type != typeof (int)).ToArray();
				return widestNumericType(types, type1, type2);
			}

			if (type1.IsUnsignedIntegerType() && type2.IsUnsignedIntegerType())
			{
				var index1 = Array.IndexOf(UnsignedIntegerTypes, type1);
				var index2 = Array.IndexOf(UnsignedIntegerTypes, type2);
				var uintIndex = Array.IndexOf(UnsignedIntegerTypes, typeof (uint));
				if (index1 < uintIndex && index2 < uintIndex)
					return typeof (int);

				return widestNumericType(UnsignedIntegerTypes, type1, type2);
			}

			// type1.IsSignedIntegerType() && type2.IsUnsignedIntegerType() or vice versa:
			return null;
		}

		private static Type widestNumericType(Type[] types, Type type1, Type type2)
		{
			var index1 = Array.IndexOf(types, type1);
			var index2 = Array.IndexOf(types, type2);
			var index = Math.Max(index1, index2);
			return types[index < 0 ? 0 : index];
		}

		private static int numericTypeConversion(Type varType, Type exprType)
		{
			if (varType.IsSignedIntegerType() && exprType.IsSignedIntegerType())
				return simpleNumericConversion(varType, exprType, SignedIntegerTypes);

			if (varType.IsUnsignedIntegerType() && exprType.IsUnsignedIntegerType())
				return simpleNumericConversion(varType, exprType, UnsignedIntegerTypes);

			if (varType.IsFloatType() && exprType.IsFloatType())
				return simpleNumericConversion(varType, exprType, FloatTypes);

			if (varType.IsSignedIntegerType() && exprType.IsUnsignedIntegerType())
				return unsignedToSignedConversion(varType, exprType);
			
			if (varType.IsFloatType() && exprType.IsSignedIntegerType())
				return signedToFloatConversion(varType, exprType);

			if (varType.IsFloatType() && exprType.IsUnsignedIntegerType())
				return unsignedToFloatConversion(varType, exprType);

			return int.MaxValue;
		}

		private static int simpleNumericConversion(Type varType, Type exprType, Type[] conversionChain)
		{
			var varTypeIndex = Array.IndexOf(conversionChain, varType);
			var exprTypeIndex = Array.IndexOf(conversionChain, exprType);
			if (varTypeIndex < exprTypeIndex)
				return int.MaxValue;

			return varTypeIndex - exprTypeIndex;
		}

		private static int unsignedToSignedConversion(Type varType, Type exprType)
		{
			// no unsigned type can be converted to the signed byte.
			if (varType == typeof (sbyte))
				return int.MaxValue;

			var index = Array.IndexOf(SignedIntegerTypes, varType);
			var correspondingUnsignedType = UnsignedIntegerTypes[index - 1]; // only expanding conversions allowed

			var result = simpleNumericConversion(correspondingUnsignedType, exprType, UnsignedIntegerTypes);
			return result == int.MaxValue
				? int.MaxValue
				: result + 1;
		}

		private static int signedToFloatConversion(Type varType, Type exprType)
		{
			var targetType = getCorrespondingSignedType(varType);

			var result = simpleNumericConversion(targetType, exprType, SignedIntegerTypes);
			return result == int.MaxValue
				? int.MaxValue
				: result + 1;
		}

		private static int unsignedToFloatConversion(Type varType, Type exprType)
		{
			if (exprType == typeof (ulong) && varType == typeof (decimal))
			{
				// ulong can be implicitly converted only to decimal.
				return 1;
			}
			else
			{
				// If type is not ulong we need to convert it to the corresponding signed type.
				var correspondingSignedType = getCorrespondingSignedType(varType);
				var result = unsignedToSignedConversion(correspondingSignedType, exprType);

				return result == int.MaxValue
					? int.MaxValue
					: result + 1;
			}
		}

		private static Type getCorrespondingSignedType(Type floatType)
		{
			if (floatType == typeof (float))
				return typeof (int);

			if (floatType == typeof (double) || floatType == typeof (decimal))
				return typeof (long);

			return null;
		}

		#endregion

		#region Type list distance

		/// <summary>
		/// Gets total distance between two sets of argument types.
		/// </summary>
		public static MethodLookupResult<T> ArgumentDistance<T>(IEnumerable<Type> passedTypes, Type[] actualTypes, T method, bool isVariadic)
		{
			if(!isVariadic)
				return new MethodLookupResult<T>(method, TypeListDistance(passedTypes, actualTypes), actualTypes);

			var simpleCount = actualTypes.Length - 1;

			var simpleDistance = TypeListDistance(passedTypes.Take(simpleCount), actualTypes.Take(simpleCount));
			var variadicDistance = variadicArgumentDistance(passedTypes.Skip(simpleCount), actualTypes[simpleCount]);
			var distance = simpleDistance == int.MaxValue || variadicDistance == int.MaxValue ? int.MaxValue : simpleDistance + variadicDistance;
			return new MethodLookupResult<T>(method, distance, actualTypes);
		}

		/// <summary>
		/// Gets total distance between two sequence of types.
		/// </summary>
		public static int TypeListDistance(IEnumerable<Type> passedArgs, IEnumerable<Type> calleeArgs)
		{
			var passedIter = passedArgs.GetEnumerator();
			var calleeIter = calleeArgs.GetEnumerator();

			var totalDist = 0;
			while (true)
			{
				var passedOk = passedIter.MoveNext();
				var calleeOk = calleeIter.MoveNext();

				// argument count differs: method cannot be applied
				if (passedOk != calleeOk)
					return int.MaxValue;

				// both sequences have finished
				if (!calleeOk)
					return totalDist;

				var dist = calleeIter.Current.DistanceFrom(passedIter.Current);
				if (dist == int.MaxValue)
					return int.MaxValue;

				totalDist += dist;
			}
		}

		/// <summary>
		/// Calculates the compound distance of a list of arguments packed into a param array.
		/// </summary>
		private static int variadicArgumentDistance(IEnumerable<Type> passedArgs, Type variadicArg)
		{
			var args = passedArgs.ToArray();

			// variadic function invoked with an array: no conversion
			if (args.Length == 1 && args[0] == variadicArg)
				return 0;

			var sum = 0;
			var elemType = variadicArg.GetElementType();

			foreach (var curr in args)
			{
				var currDist = elemType.DistanceFrom(curr);
				if (currDist == int.MaxValue)
					return int.MaxValue;

				sum += currDist;
			}

			// 1 extra distance point for packing arguments into the array:
			// otherwise fun(int) and fun(int, object[]) will have equal distance for `fun 1` and cause an ambiguity error
			return sum + 1;
		}

		#endregion

		#region Interface implementations and generic type applications

		/// <summary>
		/// Checks if a type implements an interface.
		/// </summary>
		/// <param name="type">Type to check.</param>
		/// <param name="iface">Desired interface.</param>
		/// <param name="unwindGenerics">A flag indicating that generic arguments should be discarded from both the type and the interface.</param>
		public static bool Implements(this Type type, Type iface, bool unwindGenerics)
		{
			var ifaces = type.ResolveInterfaces();
			if (type.IsInterface)
				ifaces = ifaces.Union(new[] { type }).ToArray();

			if (unwindGenerics)
			{
				for (var idx = 0; idx < ifaces.Length; idx++)
				{
					var curr = ifaces[idx];
					if (curr.IsGenericType)
						ifaces[idx] = curr.GetGenericTypeDefinition();
				}

				if(iface.IsGenericType)
					iface = iface.GetGenericTypeDefinition();
			}
			
			return ifaces.Contains(iface);
		}

		/// <summary>
		/// Finds an implementation of a generic interface.
		/// </summary>
		/// <param name="type">Type to find the implementation in.</param>
		/// <param name="iface">Desirrable interface.</param>
		/// <returns>Implementation of the generic interface or null if none.</returns>
		public static Type ResolveImplementationOf(this Type type, Type iface)
		{
			if (iface.IsGenericType && !iface.IsGenericTypeDefinition)
				iface = iface.GetGenericTypeDefinition();

			var ifaces = type.ResolveInterfaces();
			if(type.IsInterface)
				ifaces = ifaces.Union(new[] { type }).ToArray();

			return ifaces.FirstOrDefault(
				x => x == iface || (x.IsGenericType && x.GetGenericTypeDefinition() == iface)
			);
		}

		/// <summary>
		/// Resolves the common implementation of the given interface for two types.
		/// </summary>
		/// <param name="iface">Interface to find an implementation for in given types.</param>
		/// <param name="type1">First type to examine.</param>
		/// <param name="type2">First type to examine.</param>
		/// <returns>Common implementation of an interface, or null if none.</returns>
		public static Type ResolveCommonImplementationFor(this Type iface, Type type1, Type type2)
		{
			var impl1 = type1.ResolveImplementationOf(iface);
			var impl2 = type2.ResolveImplementationOf(iface);
			return impl1 == impl2 ? impl1 : null;
		}

		/// <summary>
		/// Checks if a type is (or implements) a specified type with any generic argument values given.
		/// Example: Dictionary&lt;A, B&gt; is Dictionary`2
		/// </summary>
		/// <param name="type">Closed type to test.</param>
		/// <param name="genericType">Generic type.</param>
		public static bool IsAppliedVersionOf(this Type type, Type genericType)
		{
			if (type.IsInterface && !genericType.IsInterface)
				throw new ArgumentException(string.Format("Interface {0} cannot implement a type! ({1} given).", type.FullName, genericType.FullName));

			if (!type.IsGenericType || !genericType.IsGenericType)
				return false;

			return genericType.IsInterface
				? type.Implements(genericType, true)
				: type.GetGenericTypeDefinition() == genericType.GetGenericTypeDefinition();
		}

		#endregion
	}
}
