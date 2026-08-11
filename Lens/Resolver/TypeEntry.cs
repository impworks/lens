using System;
using System.Collections.Generic;
using System.Linq;

namespace Lens.Resolver
{
    /// <summary>
    /// A type as the compiler understands it, whether or not the CLR has one yet.
    ///
    /// The compiler used to pass <see cref="System.Type"/> around as its only currency, which meant
    /// a type declared in the script was represented by its <see cref="System.Reflection.Emit.TypeBuilder"/>.
    /// A builder cannot answer questions about itself - most of its reflection surface throws
    /// <see cref="NotSupportedException"/> until the type is created - so every part of the resolver
    /// that needed to inspect one grew a hand-written fallback. This abstraction is where those
    /// answers live instead.
    ///
    /// A real <see cref="System.Type"/> is only obtained at emission, through <see cref="Materialize"/>.
    /// Nothing above emission may call it.
    /// </summary>
    internal abstract class TypeEntry
    {
        #region Identity

        /// <summary>
        /// The short name of the type, without its namespace.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// The namespace-qualified name, or null when the type has no meaningful one.
        /// </summary>
        public abstract string FullName { get; }

        /// <summary>
        /// The namespace the type is declared in, if any.
        /// </summary>
        public virtual string Namespace => null;

        #endregion

        #region Classification

        public abstract bool IsValueType { get; }
        public abstract bool IsInterface { get; }
        public abstract bool IsAbstract { get; }
        public abstract bool IsSealed { get; }
        public abstract bool IsEnum { get; }

        public virtual bool IsArray => false;
        public virtual bool IsByRef => false;
        public virtual bool IsPointer => false;

        /// <summary>
        /// Whether the type is an unsubstituted generic parameter.
        /// </summary>
        public virtual bool IsGenericParameter => false;

        /// <summary>
        /// Whether the type either declares generic parameters or is an instantiation of something
        /// that does.
        /// </summary>
        public virtual bool IsGenericType => false;

        /// <summary>
        /// Whether the type is an uninstantiated generic definition, like List&lt;&gt;.
        /// </summary>
        public virtual bool IsGenericTypeDefinition => false;

        /// <summary>
        /// Whether the type is one the script declared, as opposed to one the host provided.
        /// </summary>
        public virtual bool IsDeclared => false;

        #endregion

        #region Structure

        /// <summary>
        /// The type this one extends, or null for object, interfaces and generic parameters without
        /// a base type constraint.
        /// </summary>
        public abstract TypeEntry BaseType { get; }

        /// <summary>
        /// The element type of an array, by-ref or pointer type; null otherwise.
        /// </summary>
        public virtual TypeEntry ElementType => null;

        /// <summary>
        /// The type arguments of an instantiation, or the parameters of a definition.
        /// Empty when the type is not generic.
        /// </summary>
        public virtual TypeEntry[] GenericArguments => EmptyEntries;

        /// <summary>
        /// The generic definition this type instantiates, or null when it is not an instantiation.
        /// </summary>
        public virtual TypeEntry GenericDefinition => null;

        /// <summary>
        /// Every interface the type implements, transitively.
        ///
        /// Takes the resolution context because the answer for a generic parameter is its declared
        /// constraints, and those are a property of the compilation rather than of the type.
        /// </summary>
        public abstract TypeEntry[] GetInterfaces(TypeResolutionContext resolver);

        #endregion

        #region Construction

        /// <summary>
        /// The array type whose elements are of this type.
        /// </summary>
        public abstract TypeEntry MakeArray();

        /// <summary>
        /// The by-ref type that refers to a storage location of this type.
        /// </summary>
        public abstract TypeEntry MakeByRef();

        /// <summary>
        /// Instantiates a generic definition over the given arguments, returning the same entry
        /// every time the same instantiation is asked for.
        /// </summary>
        public abstract TypeEntry MakeGeneric(TypeResolutionContext resolver, TypeEntry[] arguments);

        #endregion

        #region Emission

        /// <summary>
        /// The CLR type that implements this entry.
        ///
        /// Only emission may call this: what it returns for a declared type is an assembly
        /// artefact, and asking for one is what forces the assembly into existence. It lives here
        /// rather than on the emit context only because there is no emit context yet - moving it
        /// there is what makes "nothing above emission materialises a type" enforceable by the
        /// compiler instead of by discipline.
        /// </summary>
        public abstract Type Materialize();

        #endregion

        #region Helpers

        protected static readonly TypeEntry[] EmptyEntries = new TypeEntry[0];

        /// <summary>
        /// Walks this type and everything it inherits from, nearest first.
        /// </summary>
        public IEnumerable<TypeEntry> SelfAndBaseTypes()
        {
            var curr = this;
            while (curr != null)
            {
                yield return curr;
                curr = curr.BaseType;
            }
        }

        /// <summary>
        /// Checks whether the two entries stand for the same type.
        /// </summary>
        public static bool Same(TypeEntry left, TypeEntry right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            return left.Equals(right);
        }

        /// <summary>
        /// Checks whether every entry in one list stands for the same type as its counterpart.
        /// </summary>
        public static bool SameAll(TypeEntry[] left, TypeEntry[] right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null || left.Length != right.Length)
                return false;

            return !left.Where((x, idx) => !Same(x, right[idx])).Any();
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return FullName ?? Name;
        }

        #endregion
    }
}
