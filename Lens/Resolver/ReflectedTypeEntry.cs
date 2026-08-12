using System;
using System.Linq;
using System.Reflection;

namespace Lens.Resolver
{
    /// <summary>
    /// A type the host provided: a real <see cref="System.Type"/> that reflection can be asked about.
    ///
    /// Everything here delegates to reflection, with the fallbacks the compiler has always needed
    /// for types that are partly made of things still being built. Those fallbacks disappear as the
    /// declared kinds of entry take over the cases that need them.
    /// </summary>
    internal sealed class ReflectedTypeEntry : TypeEntry
    {
        #region Constructor

        internal ReflectedTypeEntry(Type type)
        {
            _type = type ?? throw new ArgumentNullException(nameof(type));
        }

        #endregion

        #region Fields

        private readonly Type _type;

        #endregion

        #region Identity

        public override string Name => _type.Name;

        public override string FullName => _type.FullName;

        public override string Namespace => _type.Namespace;

        #endregion

        #region Classification

        public override bool IsValueType => _type.IsValueType;
        public override bool IsClass => _type.IsClass;
        public override bool IsInterface => _type.IsInterface;
        public override bool IsAbstract => _type.IsAbstract;
        public override bool IsSealed => _type.IsSealed;
        public override bool IsEnum => _type.IsEnum;

        public override bool IsArray => _type.IsArray;
        public override bool IsByRef => _type.IsByRef;
        public override bool IsPointer => _type.IsPointer;

        public override bool IsGenericParameter => _type.IsGenericParameter;
        public override bool IsGenericType => _type.IsGenericType;
        public override bool IsGenericTypeDefinition => _type.IsGenericTypeDefinition;

        #endregion

        #region Structure

        public override TypeEntry BaseType
        {
            get
            {
                Type baseType;
                try
                {
                    baseType = _type.BaseType;
                }
                catch (NotSupportedException)
                {
                    // an instantiation over something still being built cannot report its base
                    // type; the definition's base type with the arguments applied is the answer
                    if (!_type.IsGenericType)
                        return null;

                    var definition = _type.GetGenericTypeDefinition().BaseType;
                    if (definition == null)
                        return null;

                    baseType = GenericHelper.ApplyGenericArguments(definition, _type, false);
                }

                return baseType == null ? null : TypeEntryCache.Of(baseType);
            }
        }

        public override TypeEntry ElementType
        {
            get
            {
                if (!_type.IsArray && !_type.IsByRef && !_type.IsPointer)
                    return null;

                return TypeEntryCache.Of(_type.GetElementType());
            }
        }

        public override TypeEntry[] GenericArguments
        {
            get
            {
                if (!_type.IsGenericType)
                    return EmptyEntries;

                return _type.GetGenericArguments().Select(TypeEntryCache.Of).ToArray();
            }
        }

        public override TypeEntry GenericDefinition
        {
            get
            {
                if (!_type.IsGenericType || _type.IsGenericTypeDefinition)
                    return null;

                return TypeEntryCache.Of(_type.GetGenericTypeDefinition());
            }
        }

        public override TypeEntry[] GetInterfaces(TypeResolutionContext resolver)
        {
            return resolver.ResolveInterfaces(_type).Select(TypeEntryCache.Of).ToArray();
        }

        #endregion

        #region Generic parameters

        public override GenericParameterAttributes GenericParameterAttributes
        {
            get
            {
                if (!_type.IsGenericParameter)
                    return GenericParameterAttributes.None;

                try
                {
                    return _type.GenericParameterAttributes;
                }
                catch (NotSupportedException)
                {
                    // a parameter of a declaration that is still being built cannot report them
                    return GenericParameterAttributes.None;
                }
                catch (InvalidOperationException)
                {
                    return GenericParameterAttributes.None;
                }
            }
        }

        public override TypeEntry[] GenericParameterConstraints
        {
            get
            {
                if (!_type.IsGenericParameter)
                    return EmptyEntries;

                try
                {
                    return _type.GetGenericParameterConstraints().Select(TypeEntryCache.Of).ToArray();
                }
                catch (NotSupportedException)
                {
                    return EmptyEntries;
                }
                catch (InvalidOperationException)
                {
                    return EmptyEntries;
                }
            }
        }

        #endregion

        #region Assignability

        public override bool IsAssignableFrom(TypeResolutionContext resolver, TypeEntry other)
        {
            if (ReferenceEquals(other, null))
                return false;

            if (Same(this, other))
                return true;

            // a declared type is never assignable to a host type except through its base chain and
            // interfaces, both of which the entry model answers without touching reflection
            if (other.IsDeclared)
            {
                if (other.SelfAndBaseTypes().Any(x => Same(x, this)))
                    return true;

                return IsInterface && other.GetInterfaces(resolver).Any(x => Same(x, this));
            }

            var otherType = other.Materialize();
            try
            {
                return _type.IsAssignableFrom(otherType);
            }
            catch (NotSupportedException)
            {
                return otherType.IsSubclassOf(_type)
                       || (IsInterface && other.GetInterfaces(resolver).Any(x => Same(x, this)));
            }
        }

        #endregion

        #region Construction

        public override TypeEntry MakeGeneric(TypeResolutionContext resolver, params TypeEntry[] arguments)
        {
            return resolver.MakeGeneric(this, arguments);
        }

        #endregion

        #region Emission

        public override Type Materialize()
        {
            return _type;
        }

        #endregion

        #region Debug

        // diagnostics interpolate types into their messages, and Type.ToString() does not agree
        // with FullName for a generic instantiation. Deferring to the wrapped type keeps every
        // existing message byte-identical as more of the compiler starts handling entries.
        public override string ToString()
        {
            return _type.ToString();
        }

        #endregion

        #region Equality

        // reflection hands out canonical objects for runtime types, and TypeResolutionContext hands
        // out canonical objects for the instantiations it builds, so identity of the wrapped type is
        // the right comparison - but Type also overrides Equals, and honouring it costs nothing
        private bool Equals(ReflectedTypeEntry other)
        {
            return _type == other._type || _type.Equals(other._type);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ReflectedTypeEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _type.GetHashCode();
        }

        #endregion
    }
}
