using System;
using System.Linq;
using Lens.Compiler;
using Lens.Translations;

namespace Lens.Resolver
{
    internal static partial class GenericHelper
    {
        /// <summary>
        /// State class for the resolution of a single method's list of generic arguments.
        /// </summary>
        private class GenericResolver
        {
            #region Constructor

            public GenericResolver(TypeResolutionContext ctx, Type[] genericDefs, Type[] hints, LambdaResolver lambdaResolver)
            {
                _ctx = ctx;
                _genericDefs = genericDefs;
                _genericValues = hints ?? new Type[_genericDefs.Length];
                _isHinted = new bool[_genericDefs.Length];

                if (hints != null)
                    for (var idx = 0; idx < hints.Length; idx++)
                        _isHinted[idx] = hints[idx] != null;

                _lambdaResolver = lambdaResolver;
            }

            #endregion

            #region Fields

            /// <summary>
            /// The resolution context of the current compilation.
            /// </summary>
            private readonly TypeResolutionContext _ctx;

            /// <summary>
            /// The source list of generic argument definitions.
            /// </summary>
            private readonly Type[] _genericDefs;

            /// <summary>
            /// The calculated list of generic argument values.
            /// 
            /// Corresponds to _GenericDefs in index:
            /// 
            /// _GenericDefs[0] == typeof(T)
            /// _GenericValues[0] == typeof(int)
            /// Therefore, 'T' means 'int' for current method.
            /// </summary>
            private readonly Type[] _genericValues;

            /// <summary>
            /// Flags marking the arguments that were given explicitly at the call site.
            /// An explicit argument is authoritative: nothing inferred may contradict it.
            /// </summary>
            private readonly bool[] _isHinted;

            /// <summary>
            /// Callback for lambda argument resolving.
            /// </summary>
            private readonly LambdaResolver _lambdaResolver;

            #endregion

            #region Methods

            public Type[] Resolve(Type[] expected, Type[] actual)
            {
                ResolveRecursive(expected, actual, 0);

                // check if all generics have been resolved
                for (var idx = 0; idx < _genericDefs.Length; idx++)
                    if (_genericValues[idx] == null)
                        throw new TypeMatchException(string.Format(CompilerMessages.GenericArgumentNotResolved, _genericDefs[idx]));

                return _genericValues;
            }

            #endregion

            #region Helpers

            /// <summary>
            /// Resolves generic argument values for a method by its argument types.
            /// </summary>
            /// <param name="expectedTypes">Parameter types from method definition.</param>
            /// <param name="actualTypes">Actual types of arguments passed to the parameters.</param>
            /// <param name="depth">Recursion depth for condition checks.</param>
            private void ResolveRecursive(Type[] expectedTypes, Type[] actualTypes, int depth)
            {
                var exLen = expectedTypes?.Length ?? 0;
                var actLen = actualTypes?.Length ?? 0;

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

                            ResolveLambda(expected, actual, idx, depth);
                        }
                        else
                        {
                            var closest = FindImplementation(_ctx, expected, actual);
                            ResolveRecursive(
                                expected.GetGenericArguments(),
                                closest.GetGenericArguments(),
                                depth + 1
                            );
                        }
                    }

                    // an array parameter carries its element type, so 'T[]' against 'int[]'
                    // resolves T just like a constructed generic type would
                    else if (expected.IsArray && actual.IsArray)
                    {
                        ResolveRecursive(
                            new[] {expected.GetElementType()},
                            new[] {actual.GetElementType()},
                            depth + 1
                        );
                    }

                    else
                    {
                        for (var defIdx = 0; defIdx < _genericDefs.Length; defIdx++)
                        {
                            var def = _genericDefs[defIdx];
                            var value = _genericValues[defIdx];

                            if (expected != def)
                                continue;

                            // an explicitly given argument is not overridden by inference:
                            // the arguments are converted to it instead
                            if (_isHinted[defIdx])
                                continue;

                            if (value != null && value != actual)
                                throw new TypeMatchException(string.Format(CompilerMessages.GenericArgMismatch, def, actual, value));

                            _genericValues[defIdx] = actual;
                        }
                    }
                }
            }

            /// <summary>
            /// Resolves the lambda's input types if they are not specified.
            /// </summary>
            private void ResolveLambda(Type expected, Type actual, int lambdaPosition, int depth)
            {
                var expectedInfo = ReflectionHelper.WrapDelegate(_ctx, expected);
                var actualInfo = ReflectionHelper.WrapDelegate(_ctx, actual);

                var argTypes = new Type[actualInfo.ArgumentTypes.Length];

                // we assume that method has been resolved as matching correctly,
                // therefore no need to double-check argument count & stuff
                for (var idx = 0; idx < expectedInfo.ArgumentTypes.Length; idx++)
                {
                    var expArg = expectedInfo.ArgumentTypes[idx];
                    var actualArg = actualInfo.ArgumentTypes[idx];

                    if (actualArg == TypeEntryCache.Of(typeof(UnspecifiedType)))
                    {
                        // type is unspecified: try to infer it
                        try
                        {
                            argTypes[idx] = ApplyGenericArguments(expArg.Materialize(), _genericDefs, _genericValues);
                        }
                        catch (InvalidOperationException)
                        {
                            throw new LensCompilerException(string.Format(CompilerMessages.LambdaArgGenericsUnresolved, expArg));
                        }
                    }
                    else
                    {
                        // type is specified: use it
                        argTypes[idx] = actualArg.Materialize();
                        ResolveRecursive(
                            new[] {expArg.Materialize()},
                            new[] {actualArg.Materialize()},
                            depth + 1
                        );
                    }
                }

                if (_lambdaResolver != null)
                {
                    var lambdaReturnType = _lambdaResolver(lambdaPosition, argTypes);

                    // return type is significant for generic resolution
                    if (ContainsGenericParameter(expectedInfo.ReturnType.Materialize()))
                    {
                        ResolveRecursive(
                            new[] {expectedInfo.ReturnType.Materialize()},
                            new[] {lambdaReturnType},
                            depth + 1
                        );
                    }
                }
            }

            /// <summary>
            /// Finds the appropriate generic type in the inheritance of the actual type.
            /// </summary>
            private static Type FindImplementation(TypeResolutionContext ctx, Type desired, Type actual)
            {
                var generic = desired.GetGenericTypeDefinition();

                if (actual.IsGenericType && actual.GetGenericTypeDefinition() == generic)
                    return actual;

                // is interface
                if (desired.IsInterface)
                {
                    var matching = ctx.ResolveInterfaces(actual)
                                         .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == generic)
                                         .Where(i => TypeEntryCache.Of(i).DistanceFrom(ctx, TypeEntryCache.Of(desired)) != int.MaxValue)
                                         .Take(2)
                                         .ToList();

                    if (matching.Count == 0)
                        throw new TypeMatchException(string.Format(CompilerMessages.GenericInterfaceNotImplemented, actual, generic));

                    if (matching.Count > 1)
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
            private static bool ContainsGenericParameter(Type type)
            {
                if (type.IsGenericParameter)
                    return true;

                if (type.IsGenericType && !type.IsGenericTypeDefinition)
                {
                    foreach (var curr in type.GetGenericArguments())
                        if (ContainsGenericParameter(curr))
                            return true;
                }

                return false;
            }

            #endregion
        }
    }
}