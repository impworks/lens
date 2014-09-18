using System;
using System.Linq;
using Lens.Compiler;
using Lens.Translations;

namespace Lens.Resolver
{
	internal static partial class GenericHelper
	{
		private class GenericResolver
		{
			#region Constructor

			public GenericResolver(Type[] genericDefs, Type[] hints, LambdaResolver lambdaResolver)
			{
				_GenericDefs = genericDefs;
				_GenericValues = hints ?? new Type[_GenericDefs.Length];

				_LambdaResolver = lambdaResolver;
			}

			#endregion

			#region Fields

			/// <summary>
			/// The source list of generic argument definitions.
			/// </summary>
			private readonly Type[] _GenericDefs;

			/// <summary>
			/// The calculated list of generic argument values.
			/// 
			/// Corresponds to _GenericDefs in index:
			/// 
			/// _GenericDefs[0] == typeof(T)
			/// _GenericValues[0] == typeof(int)
			/// Therefore, 'T' means 'int' for current method.
			/// </summary>
			private Type[] _GenericValues;

			/// <summary>
			/// Callback for lambda argument resolving.
			/// </summary>
			private readonly LambdaResolver _LambdaResolver;

			#endregion

			#region Methods

			public Type[] Resolve(Type[] expected, Type[] actual)
			{
				resolveRecursive(expected, actual, 0);

				// check if all generics have been resolved
				for (var idx = 0; idx < _GenericDefs.Length; idx++)
					if (_GenericValues[idx] == null)
						throw new TypeMatchException(string.Format(CompilerMessages.GenericArgumentNotResolved, _GenericDefs[idx]));

				return _GenericValues;
			}

			#endregion

			#region Helpers

			/// <summary>
			/// Resolves generic argument values for a method by its argument types.
			/// </summary>
			/// <param name="expectedTypes">Parameter types from method definition.</param>
			/// <param name="actualTypes">Actual types of arguments passed to the parameters.</param>
			/// <param name="depth">Recursion depth for condition checks.</param>
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
						if (actual.IsLambdaType())
						{
							if (depth > 0)
								throw new InvalidOperationException("Lambda expressions cannot be nested!");

							resolveLambda(expected, actual, idx, depth);
						}
						else
						{
							var closest = findImplementation(expected, actual);
							resolveRecursive(
								expected.GetGenericArguments(),
								closest.GetGenericArguments(),
								depth + 1
							);
						}
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
			/// Resolves the lambda's input types if they are not specified.
			/// </summary>
			private void resolveLambda(Type expected, Type actual, int lambdaPosition, int depth)
			{
				var expectedInfo = ReflectionHelper.WrapDelegate(expected);
				var actualInfo = ReflectionHelper.WrapDelegate(actual);

				var argTypes = new Type[actualInfo.ArgumentTypes.Length];

				// we assume that method has been resolved as matching correctly,
				// therefore no need to double-check argument count & stuff
				for (var idx = 0; idx < expectedInfo.ArgumentTypes.Length; idx++)
				{
					var expArg = expectedInfo.ArgumentTypes[idx];
					var actualArg = actualInfo.ArgumentTypes[idx];

					if (actualArg == typeof(UnspecifiedType))
					{
						// type is unspecified: try to infer it
						try
						{
							argTypes[idx] = ApplyGenericArguments(expArg, _GenericDefs, _GenericValues);
						}
						catch (InvalidOperationException)
						{
							throw new LensCompilerException(string.Format(CompilerMessages.LambdaArgGenericsUnresolved, expArg));
						}
					}
					else
					{
						// type is specified: use it
						argTypes[idx] = actualArg;
						resolveRecursive(
							new[] { expArg },
							new[] { actualArg },
							depth + 1
						);
					}
				}

				if (_LambdaResolver != null)
				{
					var lambdaReturnType = _LambdaResolver(lambdaPosition, argTypes);

					// return type is significant for generic resolution
					if (containsGenericParameter(expectedInfo.ReturnType))
					{
						resolveRecursive(
							new[] { expectedInfo.ReturnType },
							new[] { lambdaReturnType },
							depth + 1
						);
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

			/// <summary>
			/// Recursively checks if the type has a reference to any of the generic argument types.
			/// </summary>
			private static bool containsGenericParameter(Type type)
			{
				if (type.IsGenericParameter)
					return true;

				if (type.IsGenericType && !type.IsGenericTypeDefinition)
				{
					foreach (var curr in type.GetGenericArguments())
						if (containsGenericParameter(curr))
							return true;
				}

				return false;
			}

			#endregion
		}
	}
}
