using System;
using System.Linq;
using System.Reflection;
using Lens.Compiler;
using Lens.Translations;

namespace Lens.Resolver
{
    /// <summary>
    /// Resolves generic arguments for types and methods.
    /// </summary>
    internal static partial class GenericHelper
    {
        #region Methods

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
        public static Type[] ResolveMethodGenericsByArgs(Type[] expectedTypes, Type[] actualTypes, Type[] genericDefs, Type[] hints = null, LambdaResolver lambdaResolver = null)
        {
            if (hints != null && hints.Length != genericDefs.Length)
                throw new ArgumentException("hints");

            var resolver = new GenericResolver(genericDefs, hints, lambdaResolver);
            return resolver.Resolve(expectedTypes, actualTypes);
        }

        /// <summary>
        /// Processes a type and replaces any references of generic arguments inside it with actual values.
        /// </summary>
        /// <param name="type">Type to process.</param>
        /// <param name="source">Type that contains the processed type as a generic parameter.</param>
        /// <param name="throwNotFound">Flag indicating that the error must be thrown if the generic parameter cannot be resolved.</param>
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
        /// <param name="throwNotFound">Flag indicating that the error must be thrown if the generic parameter cannot be resolved.</param>
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
                {
                    if (generics[idx] == type)
                    {
                        var result = values[idx];
                        if (result == null || result == typeof(UnspecifiedType))
                            throw new InvalidOperationException();

                        return values[idx];
                    }
                }

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
            if (args.Length != values.Length)
                throw new ArgumentOutOfRangeException(nameof(values));

            for (var idx = 0; idx < args.Length; idx++)
            {
                var arg = args[idx];
                var constr = arg.GenericParameterAttributes;
                var value = values[idx];

                if (constr.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint) && value.IsValueType)
                    throw new TypeMatchException(string.Format(CompilerMessages.GenericClassConstraintViolated, value, arg, type));

                if (constr.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
                    if (!value.IsValueType || (value.IsGenericType && value.GetGenericTypeDefinition() == typeof(Nullable<>)))
                        throw new TypeMatchException(string.Format(CompilerMessages.GenericStructConstraintViolated, value, arg, type));

                if (constr.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) && !value.HasDefaultConstructor())
                    throw new TypeMatchException(string.Format(CompilerMessages.GenericConstructorConstraintViolated, value, arg, type));

                var bases = arg.GetGenericParameterConstraints();
                foreach (var currBase in bases)
                    if (!currBase.IsExtendablyAssignableFrom(value, true))
                        throw new TypeMatchException(string.Format(CompilerMessages.GenericInheritanceConstraintViolated, value, arg, type, currBase));
            }

            return type.MakeGenericType(values);
        }

        #endregion
    }

    /// <summary>
    /// Exception thrown when generic resolver fails to resolve a type.
    /// </summary>
    public class TypeMatchException : Exception
    {
        public TypeMatchException(string msg) : base(msg)
        {
        }
    }

    /// <summary>
    /// Callback type for lambda resolution.
    /// </summary>
    internal delegate Type LambdaResolver(int lambdaPosition, Type[] argTypes);
}