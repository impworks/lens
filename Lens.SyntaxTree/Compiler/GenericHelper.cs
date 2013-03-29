using System;
using System.Linq;
using System.Reflection;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// Resolves generic arguments for types and methods.
	/// </summary>
	public static class GenericHelper
	{
		/// <summary>
		/// Resolves generic argument values for a method by its argument types.
		/// </summary>
		/// <param name="expectedTypes">Parameter types from method definition.</param>
		/// <param name="actualTypes">Actual types of arguments passed to the parameters.</param>
		/// <param name="genericDefs">Generic parameters.</param>
		/// <param name="genericValues">Result array where values for generic parameters will be put.</param>
		public static void ResolveMethodGenericsByArgs(Type[] expectedTypes, Type[] actualTypes, Type[] genericDefs, ref Type[] genericValues)
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
					ResolveMethodGenericsByArgs(
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
		/// <returns></returns>
		public static Type ApplyGenericArguments(Type type, Type generic)
		{
			return ApplyGenericArguments(
				type,
				generic.GetGenericTypeDefinition().GetGenericArguments(),
				generic.GetGenericArguments()
			);
		}

		/// <summary>
		/// Processes a type and replaces any references of generic arguments inside it with actual values.
		/// </summary>
		/// <param name="type">Type to process.</param>
		/// <param name="generics">Generic parameters that can be used in the type.</param>
		/// <param name="values">Actual values of generic parameters.</param>
		public static Type ApplyGenericArguments(Type type, Type[] generics, Type[] values)
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

				throw new ArgumentOutOfRangeException(string.Format("Generic parameter '{0}' was not found!", type));
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
