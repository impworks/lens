using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler.Entities;

namespace Lens.Resolver
{
    /// <summary>
    /// A type the script declared: a record, an algebraic type, one of its labels, or one of the
    /// classes the compiler generates for closures and pure-function caches.
    ///
    /// The whole point of this class is that it answers from the declaration rather than from the
    /// <see cref="System.Reflection.Emit.TypeBuilder"/>. A builder refuses to report its base type,
    /// its interfaces or its members until the type has been created, which is why the resolver used
    /// to be littered with NotSupportedException handlers, and why binding a script that merely
    /// mentions a record used to require having begun emitting it.
    ///
    /// <see cref="Materialize"/> is the only member that needs a builder, and reaching for it is
    /// what forces the assembly into existence.
    /// </summary>
    internal sealed class TypeEntityEntry : TypeEntry
    {
        #region Constructor

        internal TypeEntityEntry(TypeEntity entity)
        {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        }

        #endregion

        #region Fields

        /// <summary>
        /// The declaration this entry stands for.
        /// </summary>
        public readonly TypeEntity Entity;

        #endregion

        #region Identity

        // the CLR sees the arity-mangled name, and code that looks a declaration up by its emitted
        // name relies on that, so this reports what TypeBuilder.Name would
        public override string Name => Entity.MangledName;

        // a LENS declaration lives in no namespace, so its full name is just its name
        public override string FullName => Entity.MangledName;

        #endregion

        #region Classification

        // LENS declares reference types only: records, algebraic types, their labels, and the
        // classes the compiler generates. None of them is a struct, an interface or an enum, and
        // none is abstract - a label extends its parent rather than the parent being abstract.
        public override bool IsValueType => false;
        public override bool IsClass => true;
        public override bool IsInterface => false;
        public override bool IsAbstract => false;
        public override bool IsSealed => Entity.IsSealed;
        public override bool IsEnum => false;

        public override bool IsDeclared => true;

        // the entity is always the definition; an instantiation of it is a separate entry
        public override bool IsGenericType => Entity.IsGeneric;
        public override bool IsGenericTypeDefinition => Entity.IsGeneric;

        #endregion

        #region Structure

        public override TypeEntry BaseType => Entity.Parent ?? TypeEntryCache.Of<object>();

        public override TypeEntry[] GenericArguments
        {
            get
            {
                var parameters = Entity.GenericParameters;
                if (parameters == null || parameters.Count == 0)
                    return EmptyEntries;

                // before the declaration is prepared its parameters have no builders yet; step 3
                // gives them entries of their own and this stops depending on emission
                return parameters
                    .Select(x => x.Builder == null ? null : TypeEntryCache.Of(x.Builder))
                    .ToArray();
            }
        }

        public override TypeEntry[] GetInterfaces(TypeResolutionContext resolver)
        {
            var result = new List<TypeEntry>();

            if (Entity.Interfaces != null)
            {
                foreach (var iface in Entity.Interfaces)
                {
                    if (!result.Contains(iface))
                        result.Add(iface);

                    foreach (var curr in iface.GetInterfaces(resolver))
                        if (!result.Contains(curr))
                            result.Add(curr);
                }
            }

            // a label inherits whatever its parent implements
            var parent = Entity.Parent;
            if (parent != null)
            {
                foreach (var curr in parent.GetInterfaces(resolver))
                    if (!result.Contains(curr))
                        result.Add(curr);
            }

            return result.ToArray();
        }

        #endregion

        #region Assignability

        public override bool IsAssignableFrom(TypeResolutionContext resolver, TypeEntry other)
        {
            if (ReferenceEquals(other, null))
                return false;

            // no reflection anywhere here: the base chain and the interface list both come from the
            // declarations, which is what makes this work before anything has been emitted
            if (other.SelfAndBaseTypes().Any(x => Same(x, this)))
                return true;

            return IsInterface && other.GetInterfaces(resolver).Any(x => Same(x, this));
        }

        #endregion

        #region Construction

        public override TypeEntry MakeArray()
        {
            return TypeEntryCache.Of(Materialize().MakeArrayType());
        }

        public override TypeEntry MakeByRef()
        {
            return TypeEntryCache.Of(Materialize().MakeByRefType());
        }

        public override TypeEntry MakeGeneric(TypeResolutionContext resolver, params TypeEntry[] arguments)
        {
            return TypeEntryCache.Of(resolver.MakeGenericType(Materialize(), Materialize(arguments)));
        }

        #endregion

        #region Emission

        public override Type Materialize()
        {
            return Entity.MaterializeSelf();
        }

        #endregion

        #region Equality

        // one entry per declaration, so identity of the entity is the comparison
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is TypeEntityEntry other && ReferenceEquals(Entity, other.Entity);
        }

        public override int GetHashCode()
        {
            return Entity.GetHashCode();
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return Entity.MangledName;
        }

        #endregion
    }
}
