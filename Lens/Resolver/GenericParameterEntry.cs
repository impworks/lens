using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Compiler.Entities;

namespace Lens.Resolver
{
    /// <summary>
    /// A generic parameter declared in LENS code: the T of a generic function, record or algebraic
    /// type, or a copy of one forwarded into a compiler-generated class.
    ///
    /// A <see cref="System.Reflection.Emit.GenericTypeParameterBuilder"/> refuses to report its own
    /// constraints until the declaration that owns it has been created, which is why the compiler
    /// already carries a constraint model of its own and why TypeResolutionContext had to keep a map
    /// from builders back to it. This entry is that model wearing the shape of a type, so the
    /// question "what can I do with a T" is answered from the declaration instead of from an
    /// assembly artefact.
    /// </summary>
    internal sealed class GenericParameterEntry : TypeEntry
    {
        #region Constructor

        internal GenericParameterEntry(GenericParameterEntity entity)
        {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        }

        #endregion

        #region Fields

        /// <summary>
        /// The constraint model this entry stands for.
        /// </summary>
        public readonly GenericParameterEntity Entity;

        #endregion

        #region Identity

        public override string Name => Entity.Name;

        // reflection reports no full name for a generic parameter, because it does not name a type
        // until it has been substituted
        public override string FullName => null;

        #endregion

        #region Classification

        public override bool IsGenericParameter => true;

        public override bool IsDeclared => true;

        public override bool IsValueType => Entity.IsValueType;
        public override bool IsClass => !Entity.IsValueType;
        public override bool IsInterface => false;
        public override bool IsAbstract => false;
        public override bool IsSealed => false;
        public override bool IsEnum => false;

        #endregion

        #region Structure

        /// <summary>
        /// The base type constraint, or what the CLI implies when there is none: ValueType for a
        /// struct-constrained parameter, object otherwise.
        /// </summary>
        public override TypeEntry BaseType
        {
            get
            {
                if (Entity.BaseType != null)
                    return Entity.BaseType;

                return Entity.IsValueType ? TypeEntryCache.Of<ValueType>() : TypeEntryCache.Of<object>();
            }
        }

        public override TypeEntry[] GetInterfaces(TypeResolutionContext resolver)
        {
            // the transitive closure of the interface constraints, plus whatever the base type
            // constraint implements - the same set the compiler used to assemble by hand in
            // TypeResolutionContext.CollectConstraintInterfaces
            var result = new List<TypeEntry>();

            foreach (var iface in Entity.Interfaces)
            {
                if (!result.Contains(iface))
                    result.Add(iface);

                foreach (var curr in iface.GetInterfaces(resolver))
                    if (!result.Contains(curr))
                        result.Add(curr);
            }

            if (Entity.BaseType != null)
            {
                foreach (var curr in Entity.BaseType.GetInterfaces(resolver))
                    if (!result.Contains(curr))
                        result.Add(curr);
            }

            return result.ToArray();
        }

        #endregion

        #region Generic parameters

        public override GenericParameterAttributes GenericParameterAttributes
        {
            get
            {
                var result = GenericParameterAttributes.None;

                if (Entity.IsReferenceType)
                    result |= GenericParameterAttributes.ReferenceTypeConstraint;

                if (Entity.IsValueType)
                    result |= GenericParameterAttributes.NotNullableValueTypeConstraint;

                if (Entity.RequiresDefaultCtor)
                    result |= GenericParameterAttributes.DefaultConstructorConstraint;

                return result;
            }
        }

        public override TypeEntry[] GenericParameterConstraints
        {
            get
            {
                var result = new List<TypeEntry>();

                if (Entity.BaseType != null)
                    result.Add(Entity.BaseType);

                result.AddRange(Entity.Interfaces);

                return result.ToArray();
            }
        }

        #endregion

        #region Assignability

        public override bool IsAssignableFrom(TypeResolutionContext resolver, TypeEntry other)
        {
            // an unsubstituted parameter accepts only itself; every widening the language allows for
            // a T is decided by TypeExtensions against the constraints, not here
            return Same(this, other);
        }

        #endregion

        #region Construction

        public override TypeEntry MakeGeneric(TypeResolutionContext resolver, params TypeEntry[] arguments)
        {
            throw new InvalidOperationException($"Type parameter '{Entity.Name}' is not a generic definition!");
        }

        #endregion

        #region Emission

        public override Type Materialize()
        {
            if (Entity.Builder == null)
                throw new InvalidOperationException($"Type parameter '{Entity.Name}' has not been emitted yet!");

            return Entity.Builder;
        }

        #endregion

        #region Equality

        // one entry per declared parameter, so identity of the entity is the comparison
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is GenericParameterEntry other && ReferenceEquals(Entity, other.Entity);
        }

        public override int GetHashCode()
        {
            return Entity.GetHashCode();
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return Entity.Name;
        }

        #endregion
    }
}
