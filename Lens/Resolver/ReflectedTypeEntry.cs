using System;
using System.Linq;

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

        #region Construction

        public override TypeEntry MakeArray()
        {
            return TypeEntryCache.Of(_type.MakeArrayType());
        }

        public override TypeEntry MakeByRef()
        {
            return TypeEntryCache.Of(_type.MakeByRefType());
        }

        public override TypeEntry MakeGeneric(TypeResolutionContext resolver, TypeEntry[] arguments)
        {
            var args = arguments.Select(x => x.Materialize()).ToArray();
            return TypeEntryCache.Of(resolver.MakeGenericType(_type, args));
        }

        #endregion

        #region Emission

        public override Type Materialize()
        {
            return _type;
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
