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
		public static Type ResolveReturnType(MethodInfo method, Type[] args, Type[] hints = null)
		{
			if (!method.IsGenericMethod)
				return method.ReturnType;

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

				if(hint != value)
					throw new TypeMatchException(string.Format("Generic argument '{0}' was has hint type '{1}', but inferred type was '{2}'!", def, hint, value));
			}

			return applyGenerics(method.ReturnType, genericDefs, genericValues);
		}

		private static void resolveGenerics(Type[] expectedTypes, Type[] actualTypes, Type[] genericDefs, ref Type[] genericValues)
		{
			if(expectedTypes.Length != actualTypes.Length)
				throw new ArgumentException("Number of arguments does not match!");

			for (var idx = 0; idx < expectedTypes.Length; idx++)
			{
				var expected = expectedTypes[idx];
				var actual = actualTypes[idx];

				if (expected.IsGenericType)
					resolveGenerics(
						expected.GetGenericArguments(),
						actual.GetGenericArguments(),
						genericDefs,
						ref genericValues
					);

				else
				{
					for (var defIdx = 0; defIdx < genericDefs.Length; defIdx++)
					{
						var def = genericDefs[defIdx];
						var value = genericValues[defIdx];

						if (expected != def)
							continue;

						if(value != null && value != actual)
							throw new TypeMatchException(string.Format("Generic argument '{0}' has mismatched values: '{1}' and '{2}'.", def, actual, value));

						genericValues[defIdx] = actual;
					}
				}
			}
		}

		private static Type applyGenerics(Type type, Type[] genericDefs, Type[] values)
		{
			if (type.IsGenericParameter)
			{
				for (var idx = 0; idx < genericDefs.Length; idx++)
					if (type == genericDefs[idx])
						return values[idx];
			}

			if (!type.IsGenericType)
				return type;

			var args = type.GetGenericArguments().Select(a => applyGenerics(a, genericDefs, values)).ToArray();
			return type.MakeGenericType(args);
		}
	}

	public class TypeMatchException: Exception
	{
		public TypeMatchException(string msg) : base(msg)
		{ }
	}
}
