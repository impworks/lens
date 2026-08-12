using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Compiler;
using Lens.Compiler.Entities;
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
        public static Type[] ResolveMethodGenericsByArgs(TypeResolutionContext ctx, Type[] expectedTypes, Type[] actualTypes, Type[] genericDefs, Type[] hints = null, LambdaResolver lambdaResolver = null)
        {
            if (hints != null && hints.Length != genericDefs.Length)
                throw new ArgumentException("hints");

            var resolver = new GenericResolver(ctx, genericDefs, hints, lambdaResolver);
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
        public static Type MakeGenericTypeChecked(TypeResolutionContext ctx, Type type, params Type[] values)
        {
            if (!type.IsGenericTypeDefinition)
                return type;

            var args = type.GetGenericArguments();
            if (args.Length != values.Length)
                throw new ArgumentOutOfRangeException(nameof(values));

            for (var idx = 0; idx < args.Length; idx++)
                CheckConstraint(ctx, args[idx], values[idx], type);

            return ctx.MakeGenericType(type, values);
        }

        /// <summary>
        /// Ensures that actual arguments can be applied to corresponding placeholders, entirely in
        /// the entry model.
        ///
        /// The counterpart of the overload above, and the one binding uses: nothing here materializes
        /// a type, so List&lt;SomeRecord&gt; or List&lt;T&gt; can be named without the declaration
        /// that stands behind it having been emitted.
        /// </summary>
        public static TypeEntry MakeGenericTypeChecked(TypeResolutionContext ctx, TypeEntry type, params TypeEntry[] values)
        {
            if (!type.IsGenericTypeDefinition)
                return type;

            var args = type.GenericArguments;
            if (args.Length != values.Length)
                throw new ArgumentOutOfRangeException(nameof(values));

            for (var idx = 0; idx < args.Length; idx++)
                CheckConstraint(ctx, args[idx], values[idx], type);

            return ctx.MakeGeneric(type, values);
        }

        /// <summary>
        /// Ensures that inferred or explicitly given arguments satisfy the constraints of a
        /// LENS-declared generic function.
        /// </summary>
        public static void CheckConstraints(TypeResolutionContext ctx, IList<GenericParameterEntity> parameters, Type[] values)
        {
            for (var idx = 0; idx < parameters.Count; idx++)
                CheckEntityConstraint(ctx, parameters[idx], values[idx], parameters[idx].DeclarationName);
        }

        /// <summary>
        /// Ensures that inferred or explicitly given arguments satisfy the constraints of a
        /// LENS-declared generic function, with the arguments given as entries.
        ///
        /// The check is the one below, less the "new()" constraint for an argument that is itself a
        /// declaration: whether a declared type has a parameterless constructor is a question about
        /// its members, and answering it from the declaration is a separate job.
        /// </summary>
        public static void CheckConstraints(TypeResolutionContext ctx, IList<GenericParameterEntity> parameters, TypeEntry[] values)
        {
            for (var idx = 0; idx < parameters.Count; idx++)
                CheckEntityConstraint(ctx, parameters[idx], values[idx], parameters[idx].DeclarationName);
        }

        /// <summary>
        /// Ensures that a single type argument satisfies the constraints of its placeholder, with
        /// both sides given as entries.
        /// </summary>
        private static void CheckConstraint(TypeResolutionContext ctx, TypeEntry arg, TypeEntry value, object owner)
        {
            // a declaration that has not been prepared yet cannot say what its parameters are, and
            // an unspecified argument is diagnosed by the caller; neither is checkable here
            if (ReferenceEquals(arg, null) || ReferenceEquals(value, null))
                return;

            // constraints of a LENS-declared parameter live in the compiler's own model, which the
            // parameter's entry carries
            var entity = ctx.FindConstraints(arg);
            if (entity != null)
            {
                CheckEntityConstraint(ctx, entity, value, owner);
                return;
            }

            var constr = arg.GenericParameterAttributes;

            if (constr.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint) && value.IsValueType)
                throw new TypeMatchException(string.Format(CompilerMessages.GenericClassConstraintViolated, value, arg, owner));

            if (constr.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
                if (!value.IsValueType || value.IsNullableType())
                    throw new TypeMatchException(string.Format(CompilerMessages.GenericStructConstraintViolated, value, arg, owner));

            // whether a declared type has a parameterless constructor is a question about its
            // members, and answering it from the declaration is a separate job
            if (constr.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) && !value.ContainsDeclared && !value.Materialize().HasDefaultConstructor())
                throw new TypeMatchException(string.Format(CompilerMessages.GenericConstructorConstraintViolated, value, arg, owner));

            foreach (var currBase in arg.GenericParameterConstraints)
                if (!currBase.IsExtendablyAssignableFrom(ctx, value, true))
                    throw new TypeMatchException(string.Format(CompilerMessages.GenericInheritanceConstraintViolated, value, arg, owner, currBase));
        }

        /// <summary>
        /// Ensures that a type argument satisfies the constraints recorded in the compiler's model,
        /// with the argument given as an entry.
        ///
        /// The check is the CLR-side one below, less the "new()" constraint for an argument that is
        /// itself made of a declaration: whether a declared type has a parameterless constructor is
        /// a question about its members, and answering it from the declaration is a separate job.
        /// </summary>
        private static void CheckEntityConstraint(TypeResolutionContext ctx, GenericParameterEntity entity, TypeEntry value, object owner)
        {
            if (entity.IsReferenceType && value.IsValueType)
                throw new TypeMatchException(string.Format(CompilerMessages.GenericClassConstraintViolated, value, entity.Name, owner));

            if (entity.IsValueType && (!value.IsValueType || value.IsNullableType()))
                throw new TypeMatchException(string.Format(CompilerMessages.GenericStructConstraintViolated, value, entity.Name, owner));

            if (entity.RequiresDefaultCtor && !value.ContainsDeclared && !value.Materialize().HasDefaultConstructor())
                throw new TypeMatchException(string.Format(CompilerMessages.GenericConstructorConstraintViolated, value, entity.Name, owner));

            if (entity.BaseType != null && !entity.BaseType.IsExtendablyAssignableFrom(ctx, value, true))
                throw new TypeMatchException(string.Format(CompilerMessages.GenericInheritanceConstraintViolated, value, entity.Name, owner, entity.BaseType));

            foreach (var iface in entity.Interfaces)
                if (!iface.IsExtendablyAssignableFrom(ctx, value, true))
                    throw new TypeMatchException(string.Format(CompilerMessages.GenericInheritanceConstraintViolated, value, entity.Name, owner, iface));
        }

        /// <summary>
        /// Ensures that a single type argument satisfies the constraints of its placeholder.
        /// </summary>
        private static void CheckConstraint(TypeResolutionContext ctx, Type arg, Type value, object owner)
        {
            // constraints of a LENS-declared parameter cannot be read back from an unfinished
            // builder, so the compiler's own model is the only reliable source for them
            var entity = ctx.FindConstraints(arg);
            if (entity != null)
            {
                CheckEntityConstraint(ctx, entity, value, owner);
                return;
            }

            var constr = arg.GenericParameterAttributes;

            if (constr.HasFlag(GenericParameterAttributes.ReferenceTypeConstraint) && value.IsValueType)
                throw new TypeMatchException(string.Format(CompilerMessages.GenericClassConstraintViolated, value, arg, owner));

            if (constr.HasFlag(GenericParameterAttributes.NotNullableValueTypeConstraint))
                if (!value.IsValueType || TypeEntryCache.Of(value).IsNullableType())
                    throw new TypeMatchException(string.Format(CompilerMessages.GenericStructConstraintViolated, value, arg, owner));

            if (constr.HasFlag(GenericParameterAttributes.DefaultConstructorConstraint) && !value.HasDefaultConstructor())
                throw new TypeMatchException(string.Format(CompilerMessages.GenericConstructorConstraintViolated, value, arg, owner));

            foreach (var currBase in arg.GetGenericParameterConstraints())
                if (!TypeEntryCache.Of(currBase).IsExtendablyAssignableFrom(ctx, TypeEntryCache.Of(value), true))
                    throw new TypeMatchException(string.Format(CompilerMessages.GenericInheritanceConstraintViolated, value, arg, owner, currBase));
        }

        /// <summary>
        /// Ensures that a type argument satisfies the constraints recorded in the compiler's model.
        /// </summary>
        private static void CheckEntityConstraint(TypeResolutionContext ctx, GenericParameterEntity entity, Type value, object owner)
        {
            if (entity.IsReferenceType && value.IsValueType)
                throw new TypeMatchException(string.Format(CompilerMessages.GenericClassConstraintViolated, value, entity.Name, owner));

            if (entity.IsValueType && (!value.IsValueType || TypeEntryCache.Of(value).IsNullableType()))
                throw new TypeMatchException(string.Format(CompilerMessages.GenericStructConstraintViolated, value, entity.Name, owner));

            if (entity.RequiresDefaultCtor && !value.HasDefaultConstructor())
                throw new TypeMatchException(string.Format(CompilerMessages.GenericConstructorConstraintViolated, value, entity.Name, owner));

            if (entity.BaseType != null && !entity.BaseType.IsExtendablyAssignableFrom(ctx, TypeEntryCache.Of(value), true))
                throw new TypeMatchException(string.Format(CompilerMessages.GenericInheritanceConstraintViolated, value, entity.Name, owner, entity.BaseType));

            foreach (var iface in entity.Interfaces)
                if (!iface.IsExtendablyAssignableFrom(ctx, TypeEntryCache.Of(value), true))
                    throw new TypeMatchException(string.Format(CompilerMessages.GenericInheritanceConstraintViolated, value, entity.Name, owner, iface));
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