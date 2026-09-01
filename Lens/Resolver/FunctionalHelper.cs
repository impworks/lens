using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Lens.Compiler;

namespace Lens.Resolver
{
    /// <summary>
    /// A type that provides helpers for manipulating Func and Action types.
    /// </summary>
    internal static class FunctionalHelper
    {
        #region Static constructor

        static FunctionalHelper()
        {
            ActionBaseTypes = new[]
            {
                typeof(Action<>),
                typeof(Action<,>),
                typeof(Action<,,>),
                typeof(Action<,,,>),
                typeof(Action<,,,,>),
                typeof(Action<,,,,,>),
                typeof(Action<,,,,,,>),
                typeof(Action<,,,,,,,>),
                typeof(Action<,,,,,,,,>),
                typeof(Action<,,,,,,,,,>),
                typeof(Action<,,,,,,,,,,>),
                typeof(Action<,,,,,,,,,,,>),
                typeof(Action<,,,,,,,,,,,,>),
                typeof(Action<,,,,,,,,,,,,,>),
                typeof(Action<,,,,,,,,,,,,,,>),
                typeof(Action<,,,,,,,,,,,,,,,>)
            };

            FuncBaseTypes = new[]
            {
                typeof(Func<>),
                typeof(Func<,>),
                typeof(Func<,,>),
                typeof(Func<,,,>),
                typeof(Func<,,,,>),
                typeof(Func<,,,,,>),
                typeof(Func<,,,,,,>),
                typeof(Func<,,,,,,,>),
                typeof(Func<,,,,,,,,>),
                typeof(Func<,,,,,,,,,>),
                typeof(Func<,,,,,,,,,,>),
                typeof(Func<,,,,,,,,,,,>),
                typeof(Func<,,,,,,,,,,,,>),
                typeof(Func<,,,,,,,,,,,,,>),
                typeof(Func<,,,,,,,,,,,,,,>),
                typeof(Func<,,,,,,,,,,,,,,,>),
                typeof(Func<,,,,,,,,,,,,,,,,>),
            };

            LambdaBaseTypes = new[]
            {
                typeof(Lambda<>),
                typeof(Lambda<,>),
                typeof(Lambda<,,>),
                typeof(Lambda<,,,>),
                typeof(Lambda<,,,,>),
                typeof(Lambda<,,,,,>),
                typeof(Lambda<,,,,,,>),
                typeof(Lambda<,,,,,,,>),
                typeof(Lambda<,,,,,,,,>),
                typeof(Lambda<,,,,,,,,,>),
                typeof(Lambda<,,,,,,,,,,>),
                typeof(Lambda<,,,,,,,,,,,>),
                typeof(Lambda<,,,,,,,,,,,,>),
                typeof(Lambda<,,,,,,,,,,,,,>),
                typeof(Lambda<,,,,,,,,,,,,,,>),
                typeof(Lambda<,,,,,,,,,,,,,,,>),
                typeof(Lambda<,,,,,,,,,,,,,,,,>),
            };

            TupleBaseTypes = new[]
            {
                typeof(Tuple<>),
                typeof(Tuple<,>),
                typeof(Tuple<,,>),
                typeof(Tuple<,,,>),
                typeof(Tuple<,,,,>),
                typeof(Tuple<,,,,,>),
                typeof(Tuple<,,,,,,>),
                typeof(Tuple<,,,,,,,>),
            };

            ActionTypesLookup = new HashSet<Type>(ActionBaseTypes);
            FuncTypesLookup = new HashSet<Type>(FuncBaseTypes);
            LambdaTypesLookup = new HashSet<Type>(LambdaBaseTypes);
            TupleTypesLookup = new HashSet<Type>(TupleBaseTypes);
        }

        #endregion

        #region Fields

        private static readonly Type[] ActionBaseTypes;
        private static readonly Type[] FuncBaseTypes;
        private static readonly Type[] LambdaBaseTypes;
        private static readonly Type[] TupleBaseTypes;

        private static readonly HashSet<Type> ActionTypesLookup;
        private static readonly HashSet<Type> FuncTypesLookup;
        private static readonly HashSet<Type> LambdaTypesLookup;
        private static readonly HashSet<Type> TupleTypesLookup;

        #endregion

        #region Type kind methods

        /// <summary>
        /// Checks if a type is a function type.
        /// </summary>
        public static bool IsFuncType(this Type type)
        {
            return IsKnownType(FuncTypesLookup, type);
        }

        /// <summary>
        /// Checks if a type is an action type;
        /// </summary>
        public static bool IsActionType(this Type type)
        {
            return type == typeof(Action) || IsKnownType(ActionTypesLookup, type);
        }

        /// <summary>
        /// Checks if a type is a function type.
        /// </summary>
        public static bool IsLambdaType(this Type type)
        {
            return IsKnownType(LambdaTypesLookup, type);
        }

        /// <summary>
        /// Checks if a type is a tuple type.
        /// </summary>
        public static bool IsTupleType(this Type type)
        {
            return IsKnownType(TupleTypesLookup, type);
        }

        /// <summary>
        /// Checks if the type can be called.
        /// </summary>
        public static bool IsCallableType(this Type type)
        {
            while (type != null)
            {
                if (type == typeof(MulticastDelegate))
                    return true;
                type = type.BaseType;
            }

            return false;
        }

        /// <summary>
        /// Checks if a type is an <see cref="Expression{TDelegate}"/>.
        /// </summary>
        public static bool IsExpressionType(this Type type)
        {
            return type != null && type.IsGenericType && !type.IsGenericTypeDefinition && type.GetGenericTypeDefinition() == typeof(Expression<>);
        }

        /// <summary>
        /// Returns the delegate an <see cref="Expression{TDelegate}"/> stands for, or the type
        /// itself when it is not one.
        ///
        /// This is what lets a lambda be matched against a parameter that wants an expression tree
        /// rather than a delegate: everything below this point reasons about the delegate.
        /// </summary>
        public static Type UnwrapExpressionType(this Type type)
        {
            return type.IsExpressionType() ? type.GetGenericArguments()[0] : type;
        }

        #endregion

        #region Type kind methods for type entries

        // these overloads exist so that code which has already been converted to type entries can
        // ask the same questions. They defer to the reflection versions above: converting the whole
        // of this helper to entries is a separate step.
        //
        // None of these kinds is ever something the script declared - a record is not a Func, and
        // neither is a type parameter - so a declaration is answered without materialising it, which
        // would force the assembly into existence.

        /// <summary>
        /// Checks if a type is a function type.
        /// </summary>
        public static bool IsFuncType(this TypeEntry type)
        {
            return !type.IsDeclared && type.Materialize().IsFuncType();
        }

        /// <summary>
        /// Checks if a type is an action type;
        /// </summary>
        public static bool IsActionType(this TypeEntry type)
        {
            return !type.IsDeclared && (type.Is<Action>() || IsKnownType(ActionTypesLookup, type));
        }

        /// <summary>
        /// Checks if a type is a function type.
        /// </summary>
        public static bool IsLambdaType(this TypeEntry type)
        {
            return !type.IsDeclared && IsKnownType(LambdaTypesLookup, type);
        }

        /// <summary>
        /// Checks if a type is a tuple type.
        /// </summary>
        public static bool IsTupleType(this TypeEntry type)
        {
            return !type.IsDeclared && IsKnownType(TupleTypesLookup, type);
        }

        /// <summary>
        /// Checks if the type can be called.
        /// </summary>
        public static bool IsCallableType(this TypeEntry type)
        {
            // the base chain, not the CLR type: Func<SomeRecord> is as callable as Func<int>, and
            // asking either of them for a System.Type is emission's business
            return !type.IsDeclared && type.SelfAndBaseTypes().Any(x => x.Is<MulticastDelegate>());
        }

        /// <summary>
        /// Checks if an entry stands for an <see cref="Expression{TDelegate}"/>.
        /// </summary>
        public static bool IsExpressionType(this TypeEntry type)
        {
            if (ReferenceEquals(type, null) || type.IsDeclared || !type.IsGenericType || type.IsGenericTypeDefinition)
                return false;

            var definition = type.GetGenericDefinition();
            return definition != null && !definition.IsDeclared && definition.Is(typeof(Expression<>));
        }

        /// <summary>
        /// Returns the delegate an <see cref="Expression{TDelegate}"/> stands for, or the entry
        /// itself when it is not one.
        /// </summary>
        public static TypeEntry UnwrapExpressionType(this TypeEntry type)
        {
            return type.IsExpressionType() ? type.GenericArguments[0] : type;
        }

        #endregion

        #region Type constructing

        /// <summary>
        /// Creates a Func or Action depending on return type.
        /// </summary>
        public static Type CreateDelegateType(Type returnType, params Type[] args)
        {
            return TypeEntryCache.Of(returnType).IsVoid()
                ? CreateActionType(args)
                : CreateFuncType(returnType, args);
        }

        /// <summary>
        ///	Creates a new function type with argument types applied.
        /// </summary>
        public static Type CreateFuncType(Type returnType, params Type[] args)
        {
            if (args.Length > 16)
                throw new LensCompilerException("Func<> can have up to 16 arguments!");

            var baseType = FuncBaseTypes[args.Length];
            var argTypes = new List<Type>(args) {returnType};
            return baseType.MakeGenericType(argTypes.ToArray());
        }

        /// <summary>
        ///	Creates a new function type with argument types applied.
        /// </summary>
        public static Type CreateActionType(params Type[] args)
        {
            if (args.Length > 16)
                throw new LensCompilerException("Action<> can have up to 16 arguments!");

            if (args.Length == 0)
                return typeof(Action);

            var baseType = ActionBaseTypes[args.Length - 1];
            return baseType.MakeGenericType(args);
        }

        /// <summary>
        /// Creates the type of a lambda literal that has not been committed to a delegate yet.
        /// </summary>
        /// <param name="returnType">
        /// What the body produces, or <see cref="UnspecifiedType"/> when the literal cannot say -
        /// which is the case exactly when its argument types were left out, since the body cannot be
        /// bound until they are known.
        /// </param>
        public static Type CreateLambdaType(Type returnType, params Type[] args)
        {
            if (args.Length > 16)
                throw new LensCompilerException("Lambda<> can have up to 16 arguments!");

            var baseType = LambdaBaseTypes[args.Length];
            var argTypes = new List<Type>(args) {returnType};
            return baseType.MakeGenericType(argTypes.ToArray());
        }

        /// <summary>
        /// Wraps a delegate type into an <see cref="Expression{TDelegate}"/>.
        /// </summary>
        public static Type CreateExpressionType(Type delegateType)
        {
            return typeof(Expression<>).MakeGenericType(delegateType);
        }

        /// <summary>
        /// Creates a Func or Action depending on return type, in the entry model.
        ///
        /// The entry-side counterparts below exist for the same reason as the rest of the model: a
        /// lambda over a record the script declared has a perfectly ordinary type - Func&lt;Store,
        /// int&gt; - and building it through reflection would force the record's assembly into
        /// existence, which is exactly what analysis must not do.
        /// </summary>
        public static TypeEntry CreateDelegateType(TypeResolutionContext ctx, TypeEntry returnType, params TypeEntry[] args)
        {
            return returnType.IsVoid()
                ? CreateActionType(ctx, args)
                : CreateFuncType(ctx, returnType, args);
        }

        /// <summary>
        /// Creates a new function type with argument types applied, in the entry model.
        /// </summary>
        public static TypeEntry CreateFuncType(TypeResolutionContext ctx, TypeEntry returnType, params TypeEntry[] args)
        {
            if (args.Length > 16)
                throw new LensCompilerException("Func<> can have up to 16 arguments!");

            var arguments = new List<TypeEntry>(args) {returnType};
            return TypeEntryCache.Of(FuncBaseTypes[args.Length]).MakeGeneric(ctx, arguments.ToArray());
        }

        /// <summary>
        /// Creates a new action type with argument types applied, in the entry model.
        /// </summary>
        public static TypeEntry CreateActionType(TypeResolutionContext ctx, params TypeEntry[] args)
        {
            if (args.Length > 16)
                throw new LensCompilerException("Action<> can have up to 16 arguments!");

            if (args.Length == 0)
                return TypeEntryCache.Of<Action>();

            return TypeEntryCache.Of(ActionBaseTypes[args.Length - 1]).MakeGeneric(ctx, args);
        }

        /// <summary>
        /// The delegate an uncommitted lambda literal's signature describes; anything else unchanged.
        ///
        /// The marker type is the compiler's own, and a diagnostic that named it would be telling
        /// the reader about a type they cannot write and have never heard of. What they wrote is a
        /// lambda taking these arguments and producing this result, which is a Func or an Action.
        /// </summary>
        public static TypeEntry AsNamedDelegate(this TypeEntry type, TypeResolutionContext ctx)
        {
            if (!type.IsLambdaType())
                return type;

            var parts = type.GenericArguments;
            var args = new TypeEntry[parts.Length - 1];
            Array.Copy(parts, args, args.Length);

            return CreateDelegateType(ctx, parts[parts.Length - 1], args);
        }

        /// <summary>
        /// Creates the type of an uncommitted lambda literal, in the entry model.
        /// </summary>
        public static TypeEntry CreateLambdaType(TypeResolutionContext ctx, TypeEntry returnType, params TypeEntry[] args)
        {
            if (args.Length > 16)
                throw new LensCompilerException("Lambda<> can have up to 16 arguments!");

            var arguments = new List<TypeEntry>(args) {returnType};
            return TypeEntryCache.Of(LambdaBaseTypes[args.Length]).MakeGeneric(ctx, arguments.ToArray());
        }

        /// <summary>
        /// Wraps a delegate type into an <see cref="Expression{TDelegate}"/>, in the entry model.
        /// </summary>
        public static TypeEntry CreateExpressionType(TypeResolutionContext ctx, TypeEntry delegateType)
        {
            return TypeEntryCache.Of(typeof(Expression<>)).MakeGeneric(ctx, delegateType);
        }

        /// <summary>
        /// Creates a new tuple type with given argument types.
        /// </summary>
        public static Type CreateTupleType(params Type[] args)
        {
            if (args.Length > 8)
                throw new LensCompilerException("Tuple<> can have up to 8 type arguments!");

            var baseType = TupleBaseTypes[args.Length - 1];
            return baseType.MakeGenericType(args);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Checks if a type is generic and is contained in the lookup table.
        /// </summary>
        private static bool IsKnownType(HashSet<Type> typesLookup, Type type)
        {
            return type.IsGenericType && typesLookup.Contains(type.GetGenericTypeDefinition());
        }

        /// <summary>
        /// Checks whether an entry stands for one of a known family of generic types.
        ///
        /// Only the generic definition is looked at, and a definition is always a host type even when
        /// the instantiation is made of declarations - so Func&lt;SomeRecord&gt; can be recognised
        /// without anything having been emitted.
        /// </summary>
        private static bool IsKnownType(HashSet<Type> typesLookup, TypeEntry type)
        {
            if (!type.IsGenericType)
                return false;

            var definition = type.GetGenericDefinition();
            return definition != null && !definition.IsDeclared && typesLookup.Contains(definition.Materialize());
        }

        #endregion
    }
}