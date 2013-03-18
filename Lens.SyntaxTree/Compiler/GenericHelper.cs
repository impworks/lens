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
		public static Type ResolveGenericReturnType(MethodInfo method, Type[] args, Type[] hints = null)
		{
			return ResolveMethodGenerics(method, args, hints).ReturnType;
		}

		public static MethodInfo ResolveMethodGenerics(MethodInfo method, Type[] args, Type[] hints = null)
		{
			if (!method.IsGenericMethod)
				return method;

			var genericDefs = method.GetGenericArguments();
			var genericValues = new Type[genericDefs.Length];

			resolveGenerics(
				method.GetParameters().Select(p => p.ParameterType).ToArray(),
				args,
				genericDefs,
				ref genericValues
			);

			for (var idx = 0; idx < genericDefs.Length; idx++ )
			{
				var hint = hints != null ? hints[idx] : null;
				var def = genericDefs[idx];
				var value = genericValues[idx];

				if (value == null && hint == null)
					throw new TypeMatchException(string.Format("Generic argument '{0}' could not be resolved!", def));

				if(hint != null && value != null && hint != value)
					throw new TypeMatchException(string.Format("Generic argument '{0}' was has hint type '{1}', but inferred type was '{2}'!", def, hint, value));

				if (value == null)
					genericValues[idx] = hint;
			}

			return method.MakeGenericMethod(genericValues);
		}

		private static void resolveGenerics(Type[] expectedTypes, Type[] actualTypes, Type[] genericDefs, ref Type[] genericValues)
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
					resolveGenerics(
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

		private static Type findImplementation(Type desired, Type actual)
		{
			var generic = desired.GetGenericTypeDefinition();

			if (actual.IsGenericType && actual.GetGenericTypeDefinition() == generic)
				return actual;

			// is interface
			if (desired.IsInterface)
			{
				var matching = actual.GetInterfaces().Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == generic).Take(2).ToArray();
				if(matching.Length == 0)
					throw new TypeMatchException(string.Format("Type '{0}' does not implement any kind of interface '{1}'!", actual, generic));
				if(matching.Length > 1)
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
	}

	public class TypeMatchException: Exception
	{
		public TypeMatchException(string msg) : base(msg)
		{ }
	}
}
