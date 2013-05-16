using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Translations;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// Resolves generic arguments for types and methods.
	/// </summary>
	public static class GenericHelper
	{
		private static Dictionary<Type, Type[]> m_InterfaceCache = new Dictionary<Type, Type[]>();

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
					throw new TypeMatchException(string.Format(CompilerMessages.GenericArgumentNotResolved, def));

				if (hint != null && value != null && hint != value)
					throw new TypeMatchException(string.Format(CompilerMessages.GenericHintMismatch, def, hint, value));

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
				throw new ArgumentException(CompilerMessages.GenericArgCountMismatch);

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
							throw new TypeMatchException(string.Format(CompilerMessages.GenericArgMismatch, def, actual, value));

						genericValues[defIdx] = actual;
					}
				}
			}
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
				var t = ApplyGenericArguments(type.GetElementType(), generics, values);
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
				var processed = type.GetGenericArguments().Select(a => ApplyGenericArguments(a, generics, values)).ToArray();
				return def.MakeGenericType(processed);
			}

			return type;
		}

		/// <summary>
		/// Get interfaces of a possibly generic type.
		/// </summary>
		public static Type[] GetInterfaces(Type type)
		{
			if (m_InterfaceCache.ContainsKey(type))
				return m_InterfaceCache[type];

			Type[] ifaces;
			try
			{
				ifaces = type.GetInterfaces();
			}
			catch (NotSupportedException)
			{
				if (type.IsGenericType)
				{
					ifaces = type.GetGenericTypeDefinition().GetInterfaces();
					for (var idx = 0; idx < ifaces.Length; idx++)
					{
						var curr = ifaces[idx];
						if (curr.IsGenericType)
							ifaces[idx] = ApplyGenericArguments(curr, type);
					}
					
				}
				
				else if (type.IsArray)
				{
					// replace interfaces of any array with element type
					var elem = type.GetElementType();
					ifaces = typeof (int[]).GetInterfaces();
					for (var idx = 0; idx < ifaces.Length; idx++)
					{
						var curr = ifaces[idx];
						if (curr.IsGenericType)
							ifaces[idx] = curr.GetGenericTypeDefinition().MakeGenericType(elem);
					}
				}
				
				// just a built-in type : no interfaces
				else
					ifaces = Type.EmptyTypes;
			}

			m_InterfaceCache.Add(type, ifaces);
			return ifaces;
		}

		/// <summary>
		/// Ensures that actual arguments can be applied to corresponding placeholders.
		/// </summary>
		public static Type MakeGenericTypeChecked(Type type, Type[] values)
		{
			if (!type.IsGenericTypeDefinition)
				return type;

			var args = type.GetGenericArguments();
			if(args.Length != values.Length)
				throw new ArgumentOutOfRangeException("values");

			for (var idx = 0; idx < args.Length; idx++)
			{
				var arg = args[idx];
				var value = values[idx];
			}

			return type.MakeGenericType(values);
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
				var matching = GetInterfaces(actual).Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == generic).Take(2).ToArray();
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

		#endregion
	}

	public class TypeMatchException: Exception
	{
		public TypeMatchException() { }
		public TypeMatchException(string msg) : base(msg) { }
	}
}
