using System;
using System.Collections.Generic;
using System.Linq;

namespace Lens.Resolver
{
    /// <summary>
    /// A generic type applied to arguments, at least one part of which the script declared.
    ///
    /// Reflection cannot represent this usefully. Asking a TypeBuilderInstantiation for its base
    /// type, its interfaces or its members throws, and every call to MakeGenericType on a builder
    /// hands back a fresh object that compares unequal to its siblings - which is why the compiler
    /// had to intern them by hand. This entry answers instead by substituting its arguments into
    /// whatever the definition says, and its identity is the definition plus the arguments, so
    /// canonicality falls out rather than being maintained.
    /// </summary>
    internal sealed class ConstructedTypeEntry : TypeEntry
    {
        #region Constructor

        internal ConstructedTypeEntry(TypeResolutionContext resolver, TypeEntry definition, TypeEntry[] arguments)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _arguments = arguments ?? throw new ArgumentNullException(nameof(arguments));
        }

        #endregion

        #region Fields

        private readonly TypeResolutionContext _resolver;
        private readonly TypeEntry _definition;
        private readonly TypeEntry[] _arguments;

        private Type _materialized;

        #endregion

        #region Identity

        // reflection reports the definition's arity-mangled short name for an instantiation
        public override string Name => _definition.Name;

        public override string FullName => _definition.FullName == null
            ? null
            : _definition.FullName + "[" + string.Join(",", _arguments.Select(x => x.FullName ?? x.Name)) + "]";

        public override string Namespace => _definition.Namespace;

        #endregion

        #region Classification

        public override bool IsValueType => _definition.IsValueType;
        public override bool IsClass => _definition.IsClass;
        public override bool IsInterface => _definition.IsInterface;
        public override bool IsAbstract => _definition.IsAbstract;
        public override bool IsSealed => _definition.IsSealed;
        public override bool IsEnum => _definition.IsEnum;

        // Span<SomeRecord> is a ref struct for the same reason Span<int> is: the property belongs
        // to the definition, and no argument can take it away
        public override bool IsByRefLike => _definition.IsByRefLike;

        public override bool IsGenericType => true;
        public override bool IsGenericTypeDefinition => false;

        // the instantiation itself is not a declaration, but it is made of at least one
        public override bool ContainsDeclared => true;

        #endregion

        #region Structure

        public override TypeEntry GenericDefinition => _definition;

        public override TypeEntry[] GenericArguments => _arguments;

        public override TypeEntry BaseType
        {
            get
            {
                var definitionBase = _definition.BaseType;
                return definitionBase == null ? null : Substitute(definitionBase);
            }
        }

        public override TypeEntry[] GetInterfaces(TypeResolutionContext resolver)
        {
            return _definition.GetInterfaces(resolver).Select(Substitute).ToArray();
        }

        #endregion

        #region Substitution

        /// <summary>
        /// Rewrites a type expressed in terms of the definition's parameters into the same type
        /// expressed in terms of this instantiation's arguments.
        /// </summary>
        private TypeEntry Substitute(TypeEntry type)
        {
            return SubstituteInto(_resolver, type, _definition.GenericArguments, _arguments);
        }

        /// <summary>
        /// Replaces every occurrence of the given parameters with the corresponding argument,
        /// walking into arrays, by-ref types and nested instantiations.
        ///
        /// This is the entry-space counterpart of GenericHelper.ApplyGenericArguments, and unlike it
        /// needs nothing to have been emitted.
        /// </summary>
        public static TypeEntry SubstituteInto(TypeResolutionContext resolver, TypeEntry type, TypeEntry[] parameters, TypeEntry[] arguments)
        {
            if (ReferenceEquals(type, null))
                return null;

            if (type.IsGenericParameter)
            {
                for (var idx = 0; idx < parameters.Length && idx < arguments.Length; idx++)
                    if (Same(parameters[idx], type))
                        return arguments[idx];

                return type;
            }

            if (type.IsArray)
            {
                var element = SubstituteInto(resolver, type.ElementType, parameters, arguments);
                return Same(element, type.ElementType) ? type : element.MakeArray(resolver, type.ArrayRank);
            }

            if (type.IsByRef)
            {
                var element = SubstituteInto(resolver, type.ElementType, parameters, arguments);
                return Same(element, type.ElementType) ? type : element.MakeByRef(resolver);
            }

            // a definition is included deliberately: a member signature routinely names its own
            // declaring type as the open form - the type of EqualityComparer<>.Default, and the
            // argument of its GetHashCode, are both spelled that way - and skipping those made
            // substitution silently do nothing and resolve the member on the open definition
            if (type.IsGenericType)
            {
                var args = type.GenericArguments;
                var substituted = new TypeEntry[args.Length];
                var changed = false;

                for (var idx = 0; idx < args.Length; idx++)
                {
                    substituted[idx] = SubstituteInto(resolver, args[idx], parameters, arguments);
                    changed |= !Same(substituted[idx], args[idx]);
                }

                return changed ? type.GetGenericDefinition().MakeGeneric(resolver, substituted) : type;
            }

            return type;
        }

        #endregion

        #region Assignability

        public override bool IsAssignableFrom(TypeResolutionContext resolver, TypeEntry other)
        {
            if (ReferenceEquals(other, null))
                return false;

            if (Same(this, other))
                return true;

            if (other.SelfAndBaseTypes().Any(x => Same(x, this)))
                return true;

            return IsInterface && other.GetInterfaces(resolver).Any(x => Same(x, this));
        }

        #endregion

        #region Construction

        public override TypeEntry MakeGeneric(TypeResolutionContext resolver, params TypeEntry[] arguments)
        {
            throw new InvalidOperationException($"Type '{Name}' is already constructed!");
        }

        #endregion

        #region Emission

        public override Type Materialize()
        {
            // the CLR type is built once and only when somebody actually needs one, which is what
            // keeps a mention of List<SomeRecord> from forcing the record into the assembly
            return _materialized ?? (_materialized = _resolver.MakeGenericType(_definition.Materialize(), Materialize(_arguments)));
        }

        #endregion

        #region Equality

        private bool Equals(ConstructedTypeEntry other)
        {
            return Same(_definition, other._definition) && SameAll(_arguments, other._arguments);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ConstructedTypeEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = _definition.GetHashCode();
                foreach (var curr in _arguments)
                    hash = (hash * 397) ^ (curr?.GetHashCode() ?? 0);

                return hash;
            }
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            // reflection spells a definition with its arity mangled into the name and its arguments
            // in brackets after it - 'List`1[T]' - which is neither how the script wrote it nor how
            // anybody reading an error message expects to see it
            var name = _definition.FullName ?? _definition.Name;

            var arity = name.IndexOf('`');
            if (arity >= 0)
                name = name.Substring(0, arity);

            return name + "<" + string.Join(", ", _arguments.Select(x => x.ToString())) + ">";
        }

        #endregion
    }
}
