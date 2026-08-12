using System;
using System.Collections.Generic;
using System.Linq;

namespace Lens.Resolver
{
    /// <summary>
    /// An array whose element type the script declared, or is made of something the script declared.
    ///
    /// Exists for the same reason as <see cref="ConstructedTypeEntry"/>: MakeArrayType on a builder
    /// produces something reflection cannot answer questions about, and 'T[]' in the signature of a
    /// generic declaration is entirely ordinary LENS.
    /// </summary>
    internal sealed class ArrayTypeEntry : TypeEntry
    {
        #region Constructor

        internal ArrayTypeEntry(TypeResolutionContext resolver, TypeEntry element)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _element = element ?? throw new ArgumentNullException(nameof(element));
        }

        #endregion

        #region Fields

        private readonly TypeResolutionContext _resolver;
        private readonly TypeEntry _element;

        private Type _materialized;

        #endregion

        #region Identity

        public override string Name => _element.Name + "[]";

        public override string FullName => _element.FullName == null ? null : _element.FullName + "[]";

        public override string Namespace => _element.Namespace;

        #endregion

        #region Classification

        public override bool IsArray => true;
        public override bool IsValueType => false;
        public override bool IsClass => true;
        public override bool IsInterface => false;
        public override bool IsAbstract => false;
        public override bool IsSealed => true;
        public override bool IsEnum => false;

        public override bool ContainsDeclared => true;

        #endregion

        #region Structure

        public override TypeEntry ElementType => _element;

        public override TypeEntry BaseType => TypeEntryCache.Of<Array>();

        public override TypeEntry[] GetInterfaces(TypeResolutionContext resolver)
        {
            // an array implements the same interfaces int[] does, over its own element type - which
            // is how the compiler has always answered this for arrays it could not reflect on
            var result = new List<TypeEntry>();

            foreach (var iface in TypeEntryCache.Of<int[]>().GetInterfaces(resolver))
            {
                result.Add(
                    iface.IsGenericType
                        ? iface.GetGenericDefinition().MakeGeneric(resolver, _element)
                        : iface
                );
            }

            return result.ToArray();
        }

        #endregion

        #region Assignability

        public override bool IsAssignableFrom(TypeResolutionContext resolver, TypeEntry other)
        {
            return Same(this, other);
        }

        #endregion

        #region Construction

        public override TypeEntry MakeGeneric(TypeResolutionContext resolver, params TypeEntry[] arguments)
        {
            throw new InvalidOperationException($"Type '{Name}' is not a generic definition!");
        }

        #endregion

        #region Emission

        public override Type Materialize()
        {
            return _materialized ?? (_materialized = _element.Materialize().MakeArrayType());
        }

        #endregion

        #region Equality

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ArrayTypeEntry other && Same(_element, other._element);
        }

        public override int GetHashCode()
        {
            return unchecked(_element.GetHashCode() * 397) ^ 1;
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return _element + "[]";
        }

        #endregion
    }

    /// <summary>
    /// A by-ref type whose element type the script declared, or is made of something it declared.
    /// </summary>
    internal sealed class ByRefTypeEntry : TypeEntry
    {
        #region Constructor

        internal ByRefTypeEntry(TypeResolutionContext resolver, TypeEntry element)
        {
            _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
            _element = element ?? throw new ArgumentNullException(nameof(element));
        }

        #endregion

        #region Fields

        private readonly TypeResolutionContext _resolver;
        private readonly TypeEntry _element;

        private Type _materialized;

        #endregion

        #region Identity

        public override string Name => _element.Name + "&";

        public override string FullName => _element.FullName == null ? null : _element.FullName + "&";

        public override string Namespace => _element.Namespace;

        #endregion

        #region Classification

        public override bool IsByRef => true;
        public override bool IsValueType => false;
        public override bool IsClass => false;
        public override bool IsInterface => false;
        public override bool IsAbstract => false;
        public override bool IsSealed => false;
        public override bool IsEnum => false;

        public override bool ContainsDeclared => true;

        #endregion

        #region Structure

        public override TypeEntry ElementType => _element;

        public override TypeEntry BaseType => null;

        public override TypeEntry[] GetInterfaces(TypeResolutionContext resolver)
        {
            return EmptyEntries;
        }

        #endregion

        #region Assignability

        public override bool IsAssignableFrom(TypeResolutionContext resolver, TypeEntry other)
        {
            return Same(this, other);
        }

        #endregion

        #region Construction

        public override TypeEntry MakeGeneric(TypeResolutionContext resolver, params TypeEntry[] arguments)
        {
            throw new InvalidOperationException($"Type '{Name}' is not a generic definition!");
        }

        #endregion

        #region Emission

        public override Type Materialize()
        {
            return _materialized ?? (_materialized = _element.Materialize().MakeByRefType());
        }

        #endregion

        #region Equality

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ByRefTypeEntry other && Same(_element, other._element);
        }

        public override int GetHashCode()
        {
            return unchecked(_element.GetHashCode() * 397) ^ 2;
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return _element + "&";
        }

        #endregion
    }
}
