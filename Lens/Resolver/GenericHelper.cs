using System;
using System.Linq;
using System.Reflection;
using Lens.Translations;

namespace Lens.Resolver
{
	/// <summary>
	/// Resolves generic arguments for types and methods.
	/// </summary>
	internal static class GenericHelper
	{
		/// <summary>
		/// Resolves the generic values for a specified type.
		/// </summary>
		/// <param name="expectedTypes">Parameter types from method definition.</param>
		/// <param name="actualTypes">Argument types from method invocation site. </param>
		/// <param name="genericDefs">Generic parameters from method definition.</param>
		/// <param name="hints">Extra hints that are specified explicitly.</param>
		/// <param name="lambdaResolver">
		/// Callback for Lambda`T resolution.
		/// Passed arguments are:
		/// 1. Lambda's position in the argument list (to find a corresponding NodeBase)
		/// 2. Already resolved list of types
		/// Return value is the inferred type of lambda return.
		/// </param>
		/// <returns></returns>
		public static Type[] ResolveMethodGenericsByArgs(Type[] expectedTypes, Type[] actualTypes, Type[] genericDefs, Type[] hints = null, Func<int, Type[], Type> lambdaResolver = null)
		{
			if(hints != null && hints.Length != genericDefs.Length)
				throw new ArgumentException("hints");

			var resolver = new GenericResolver(genericDefs, hints, lambdaResolver);
			return resolver.Resolve(expectedTypes, actualTypes);
		}

		/// <summary>
		/// Processes a type and replaces any references of generic arguments inside it with actual values.
		/// </summary>
		/// <param name="type">Type to process.</param>
		/// <param name="source">Type that contains the processed type as a generic parameter.</param>
		public static Type ApplyGenericArguments(Type type, Type source, bool throwNotFound = true)
		{
			if (source.IsGenericType)
			{
				return ApplyGenericArguments(
					type,
					source.GetGenericTypeDefinition().GetGenericArguments(),
					source.GetGenericArguments(),
					throwNotFound
				);
			}

			if (source.IsArray && type.IsGenericType)
			{
				return ApplyGenericArguments(
					type,
					new[] {type.GetGenericArguments()[0]},
					new[] {source.GetElementType()},
					throwNotFound
				);
			}

			return type;
		}

		/// <summary>
		/// Processes a type and replaces any references of generic arguments inside it with actual values.
		/// </summary>
		/// <param name="type">Type to process.</param>
		/// <param name="generics">Generic parameters that can be used in the type.</param>
		/// <param name="values">Actual values of generic parameters.</param>
		public static Type ApplyGenericArguments(Type type, Type[] generics, Type[] values, bool throwNotFound = true)
		{
			if (type.IsArray || type.IsByRef)
			{
				var t = ApplyGenericArguments(type.GetElementType(), generics, values, throwNotFound);
				return type.IsArray ? t.MakeArrayType() : t.MakeByRefType();
			}

			if (type.IsGenericParameter)
			{
				for (var idx = 0; idx < generics.Length; idx++)
					if (generics[idx] == type)
						return values[idx];

				if (throwNotFound)
					throw new ArgumentOutOfRangeException(string.Format(CompilerMessages.GenericParameterNotFound, type));

				return type;
			}

			if (type.IsGenericType)
			{
				var def = type.GetGenericTypeDefinition();
				var processed = type.GetGenericArguments().Select(a => ApplyGenericArguments(a, generics, values, throwNotFound)).ToArray();
				return def.MakeGenericType(processed);
			}

			return type;
		}

		/// <summary>
		/// Ensures that actual arguments can be applied to corresponding placeholders.
		/// </summary>
		public static Type MakeGenericTypeChecked(Type type, params Type[] values)
		{
			if (!type.IsGenericTypeDefinition)
				return type;

			var args = type.GetGenericArguments();
			if(args.Length != values.Length)
				throw new ArgumentOutOfRangeException("values");

			for (var idx = 0; idx < args.Length; idx++)
			{
				var arg = args[idx];
				var constr = arg.GenericParameterAttributes;
				var value = values[idx];

				if (constr.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint) && value.IsValueType)
					throw new TypeMatchException(string.Format(CompilerMessages.GenericClassConstraintViolated, value, arg, type));

				if (constr.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
					if(!value.IsValueType || (value.IsGenericType && value.GetGenericTypeDefinition() == typeof(Nullable<>)))
						throw new TypeMatchException(string.Format(CompilerMessages.GenericStructConstraintViolated, value, arg, type));

				if (constr.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) && !value.HasDefaultConstructor())
					throw new TypeMatchException(string.Format(CompilerMessages.GenericConstructorConstraintViolated, value, arg, type));

				var bases = arg.GetGenericParameterConstraints();
				foreach (var currBase in bases)
					if(!currBase.IsExtendablyAssignableFrom(value, true))
						throw new TypeMatchException(string.Format(CompilerMessages.GenericInheritanceConstraintViolated, value, arg, type, currBase));
			}

			return type.MakeGenericType(values);
		}

		private class GenericResolver
		{
			public GenericResolver(Type[] genericDefs, Type[] hints, Func<int, Type[], Type> lambdaResolver)
			{
				_GenericDefs = genericDefs;
				_GenericValues = hints ?? new Type[_GenericDefs.Length];

				_LambdaResolver = lambdaResolver;
			}

			private readonly Type[] _GenericDefs;
			private Type[] _GenericValues;
			private readonly Func<int, Type[], Type> _LambdaResolver;

			public Type[] Resolve(Type[] expected, Type[] actual)
			{
				resolveRecursive(expected, actual, 0);
				// check if all generics have been resolved

				for (var idx = 0; idx < _GenericDefs.Length; idx++)
					if (_GenericValues[idx] == null)
						throw new TypeMatchException(string.Format(CompilerMessages.GenericArgumentNotResolved, _GenericDefs[idx]));

				return _GenericValues;
			}

			/// <summary>
			/// Resolves generic argument values for a method by its argument types.
			/// </summary>
			/// <param name="expectedTypes">Parameter types from method definition.</param>
			/// <param name="actualTypes">Actual types of arguments passed to the parameters.</param>
			private void resolveRecursive(Type[] expectedTypes, Type[] actualTypes, int depth)
			{
				var exLen = expectedTypes != null ? expectedTypes.Length : 0;
				var actLen = actualTypes != null ? actualTypes.Length : 0;

				if (exLen != actLen)
					throw new ArgumentException(CompilerMessages.GenericArgCountMismatch);

				for (var idx = 0; idx < exLen; idx++)
				{
					var expected = expectedTypes[idx];
					var actual = actualTypes[idx];

					if (expected.IsGenericType)
					{
						// todo
						if (actual.IsLambdaType())
							continue;

						var closest = findImplementation(expected, actual);
						resolveRecursive(
							expected.GetGenericArguments(),
							closest.GetGenericArguments(),
							depth + 1
						);
					}

					else
					{
						for (var defIdx = 0; defIdx < _GenericDefs.Length; defIdx++)
						{
							var def = _GenericDefs[defIdx];
							var value = _GenericValues[defIdx];

							if (expected != def)
								continue;

							if (value != null && value != actual)
								throw new TypeMatchException(string.Format(CompilerMessages.GenericArgMismatch, def, actual, value));

							_GenericValues[defIdx] = actual;
						}
					}
				}
			}

			/// <summary>
			/// Finds the appropriate generic type in the inheritance of the actual type.
			/// </summary>
			private static Type findImplementation(Type desired, Type actual)
			{
				var generic = desired.GetGenericTypeDefinition();

				if (actual.IsGenericType && actual.GetGenericTypeDefinition() == generic)
					return actual;

				// is interface
				if (desired.IsInterface)
				{
					var matching = actual.ResolveInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == generic).Take(2).ToArray();
					if (matching.Length == 0)
						throw new TypeMatchException(string.Format(CompilerMessages.GenericInterfaceNotImplemented, actual, generic));
					if (matching.Length > 1)
						throw new TypeMatchException(string.Format(CompilerMessages.GenericInterfaceMultipleImplementations, generic, actual));

					return matching[0];
				}

				// is inherited
				var currType = actual;
				while (currType != null)
				{
					if (currType.IsGenericType && currType.GetGenericTypeDefinition() == generic)
						return currType;

					currType = currType.BaseType;
				}

				throw new TypeMatchException(string.Format(CompilerMessages.GenericImplementationWrongType, generic, actual));
			}
		}
	}

	public class TypeMatchException: Exception
	{
		public TypeMatchException() { }
		public TypeMatchException(string msg) : base(msg) { }
	}
}
