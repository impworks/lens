using System;
using System.Linq;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// Resolves generic arguments for types and methods.
	/// </summary>
	public static class GenericHelper
	{
		public static Type[] ResolveMethodGenericsByArgs(Type[] expectedTypes, Type[] actualTypes, Type[] genericDefs, Type[] hints = null)
		{
			var genericValues = new Type[genericDefs.Length];

			resolveMethodGenericsByArgs(expectedTypes, actualTypes, genericDefs, ref genericValues);

			for (var idx = 0; idx < genericDefs.Length; idx++)
			{
				var hint = hints != null ? hints[idx] : null;
				var def = genericDefs[idx];
				var value = genericValues[idx];

				if (value == null && hint == null)
					throw new TypeMatchException(string.Format("Generic argument '{0}' could not be resolved!", def));

				if (hint != null && value != null && hint != value)
					throw new TypeMatchException(string.Format("Generic argument '{0}' was has hint type '{1}', but inferred type was '{2}'!", def, hint, value));

				if (value == null)
					genericValues[idx] = hint;
			}

			return genericValues;
		}

		/// <summary>
		/// Resolves generic argument values for a method by its argument types.
		/// </summary>
		/// <param name="expectedTypes">Parameter types from method definition.</param>
		/// <param name="actualTypes">Actual types of arguments passed to the parameters.</param>
		/// <param name="genericDefs">Generic parameters.</param>
		/// <param name="genericValues">Result array where values for generic parameters will be put.</param>
		private static void resolveMethodGenericsByArgs(Type[] expectedTypes, Type[] actualTypes, Type[] genericDefs, ref Type[] genericValues)
		{
			var exLen = expectedTypes != null ? expectedTypes.Length : 0;
			var actLen = actualTypes != null ? actualTypes.Length : 0;

			if(exLen != actLen)
				throw new ArgumentException("Number of arguments does not match!");

			for (var idx = 0; idx < exLen; idx++)
			{
				var expected = expectedTypes[idx];
				var actual = actualTypes[idx];

				if (expected.IsGenericType)
				{
					var closest = findImplementation(expected, actual);
					resolveMethodGenericsByArgs(
						expected.GetGenericArguments(),
						closest.GetGenericArguments(),
						genericDefs,
						ref genericValues
					);
				}

				else
				{
					for (var defIdx = 0; defIdx < genericDefs.Length; defIdx++)
					{
						var def = genericDefs[defIdx];
						var value = genericValues[defIdx];

						if (expected != def)
							continue;

						if (value != null && value != actual)
							throw new TypeMatchException(string.Format("Generic argument '{0}' has mismatched values: '{1}' and '{2}'.", def, actual, value));

						genericValues[defIdx] = actual;
					}
				}
			}
		}

		/// <summary>
		/// Processes a type and replaces any references of generic arguments inside it with actual values.
		/// </summary>
		/// <param name="type">Type to process.</param>
		/// <param name="generic">Type that contains the processed type as a generic parameter.</param>
		public static Type ApplyGenericArguments(Type type, Type generic, bool throwNotFound = true)
		{
			if (!generic.IsGenericType)
				return type;

			return ApplyGenericArguments(
				type,
				generic.GetGenericTypeDefinition().GetGenericArguments(),
				generic.GetGenericArguments(),
				throwNotFound
			);
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
				var t = ApplyGenericArguments(type.GetElementType(), generics, values);
				return type.IsArray ? t.MakeArrayType() : t.MakeByRefType();
			}

			if (type.IsGenericParameter)
			{
				for (var idx = 0; idx < generics.Length; idx++)
					if (generics[idx] == type)
						return values[idx];

				if (throwNotFound)
					throw new ArgumentOutOfRangeException(string.Format("Generic parameter '{0}' was not found!", type));

				return type;
			}

			if (type.IsGenericType)
			{
				var def = type.GetGenericTypeDefinition();
				var processed = type.GetGenericArguments().Select(a => ApplyGenericArguments(a, generics, values)).ToArray();
				return def.MakeGenericType(processed);
			}

			return type;
		}

		#region Helpers

		private static Type findImplementation(Type desired, Type actual)
		{
			var generic = desired.GetGenericTypeDefinition();

			if (actual.IsGenericType && actual.GetGenericTypeDefinition() == generic)
				return actual;

			// is interface
			if (desired.IsInterface)
			{
				var matching = actual.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == generic).Take(2).ToArray();
				if (matching.Length == 0)
					throw new TypeMatchException(string.Format("Type '{0}' does not implement any kind of interface '{1}'!", actual, generic));
				if (matching.Length > 1)
					throw new TypeMatchException(string.Format("Cannot infer argument types of '{0}': type '{1}' implements multiple overrides!", generic, actual));

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

			throw new TypeMatchException(string.Format("Cannot resolve arguments of '{0}' using type '{1}'!", generic, actual));
		}

		#endregion
	}

	public class TypeMatchException: Exception
	{
		public TypeMatchException(string msg) : base(msg) { }
	}
}
