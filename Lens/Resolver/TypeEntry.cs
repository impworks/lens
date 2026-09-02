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
        public abstract bool IsClass { get; }
        public abstract bool IsInterface { get; }
        public abstract bool IsAbstract { get; }
        public abstract bool IsSealed { get; }
        public abstract bool IsEnum { get; }

        public virtual bool IsArray => false;
        public virtual bool IsByRef => false;
        public virtual bool IsPointer => false;

        /// <summary>
        /// The number of dimensions of an array type, or 0 when the type is not an array.
        /// </summary>
        public virtual int ArrayRank => 0;

        /// <summary>
        /// Whether the type is a single-dimensional zero-based array: the only shape the
        /// ldelem / stelem / ldlen opcodes can address.
        /// </summary>
        public bool IsVectorArray => IsArray && ArrayRank == 1;

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

        /// <summary>
        /// Whether the type is a declaration or is built out of one: SomeRecord, List&lt;SomeRecord&gt;,
        /// T[], Dictionary&lt;string, T&gt;.
        ///
        /// This is the test for "reflection cannot be trusted with this one". A host type made only
        /// of host types can always be reflected on; anything else has to be answered by the model.
        /// </summary>
        public virtual bool ContainsDeclared
        {
            get
            {
                if (IsDeclared)
                    return true;

                var element = ElementType;
                if (!ReferenceEquals(element, null))
                    return element.ContainsDeclared;

                foreach (var curr in GenericArguments)
                    if (!ReferenceEquals(curr, null) && curr.ContainsDeclared)
                        return true;

                return false;
            }
        }

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
        ///
        /// Note the difference from <see cref="System.Type.GetGenericTypeDefinition"/>, which
        /// returns a definition unchanged when asked. Use <see cref="GetGenericDefinition"/> when
        /// translating code that relied on that.
        /// </summary>
        public virtual TypeEntry GenericDefinition => null;

        /// <summary>
        /// The generic definition behind this type, with the semantics of
        /// <see cref="System.Type.GetGenericTypeDefinition"/>: a definition is its own definition.
        ///
        /// The compiler passes open definitions around constantly - typeof(IEnumerable&lt;&gt;) and
        /// friends - so the distinction matters at nearly every call site.
        /// </summary>
        public TypeEntry GetGenericDefinition()
        {
            return IsGenericTypeDefinition ? this : GenericDefinition;
        }

        /// <summary>
        /// Every interface the type implements, transitively.
        ///
        /// Takes the resolution context because the answer for a generic parameter is its declared
        /// constraints, and those are a property of the compilation rather than of the type.
        /// </summary>
        public abstract TypeEntry[] GetInterfaces(TypeResolutionContext resolver);

        #endregion

        #region Generic parameters

        /// <summary>
        /// The keyword constraints of a generic parameter: class, struct, new().
        ///
        /// A <see cref="System.Reflection.Emit.GenericTypeParameterBuilder"/> refuses to report
        /// these until its owner is created, which is why the compiler carries a constraint model of
        /// its own. For a declared parameter that model is the answer.
        /// </summary>
        public virtual System.Reflection.GenericParameterAttributes GenericParameterAttributes =>
            System.Reflection.GenericParameterAttributes.None;

        /// <summary>
        /// The type constraints of a generic parameter, base type first if there is one.
        /// </summary>
        public virtual TypeEntry[] GenericParameterConstraints => EmptyEntries;

        #endregion

        #region Assignability

        /// <summary>
        /// Whether a value of the given type can be stored in a location of this type without any
        /// conversion. This is CLR assignability, not LENS assignability - the widening and lambda
        /// rules live in TypeExtensions.
        /// </summary>
        public abstract bool IsAssignableFrom(TypeResolutionContext resolver, TypeEntry other);

        /// <summary>
        /// Whether this type inherits from the given one, directly or transitively.
        /// </summary>
        public bool IsSubclassOf(TypeEntry other)
        {
            if (ReferenceEquals(other, null))
                return false;

            var curr = BaseType;
            while (!ReferenceEquals(curr, null))
            {
                if (Same(curr, other))
                    return true;

                curr = curr.BaseType;
            }

            return false;
        }

        #endregion

        #region Construction

        /// <summary>
        /// The array type whose elements are of this type.
        ///
        /// Goes through the resolution context because the answer is a different kind of entry
        /// depending on the element: reflection can represent int[], but not SomeRecord[].
        /// </summary>
        /// <param name="rank">
        /// The number of dimensions. Rank 1 is a vector; anything above it is a multidimensional
        /// array, which is a different kind of type with different instructions behind it.
        /// </param>
        public TypeEntry MakeArray(TypeResolutionContext resolver, int rank = 1)
        {
            return resolver.MakeArray(this, rank);
        }

        /// <summary>
        /// The by-ref type that refers to a storage location of this type.
        /// </summary>
        public TypeEntry MakeByRef(TypeResolutionContext resolver)
        {
            return resolver.MakeByRef(this);
        }

        /// <summary>
        /// Instantiates a generic definition over the given arguments, returning the same entry
        /// every time the same instantiation is asked for.
        /// </summary>
        public abstract TypeEntry MakeGeneric(TypeResolutionContext resolver, params TypeEntry[] arguments);

        /// <summary>
        /// Instantiates a host generic definition named at the call site.
        /// Spares the caller the TypeEntryCache.Of around every typeof(Something&lt;&gt;).
        /// </summary>
        public static TypeEntry Generic(TypeResolutionContext resolver, Type definition, params TypeEntry[] arguments)
        {
            return TypeEntryCache.Of(definition).MakeGeneric(resolver, arguments);
        }

        /// <summary>
        /// The nullable type that lifts this one. Pairs with GetNullableUnderlyingType.
        /// </summary>
        public TypeEntry MakeNullable(TypeResolutionContext resolver)
        {
            return Generic(resolver, typeof(Nullable<>), this);
        }

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

        #region Equality

        // the compiler compares types constantly, and the overwhelming majority of those
        // comparisons are spelled '=='. Leaving that as reference comparison would work only for as
        // long as every entry is canonical, and an instantiation assembled from parts is not, so
        // the operators are wired to Equals rather than left to their default.

        // WARNING for anything written inside this class hierarchy: 'entry == null' calls the
        // operator below, so testing an entry against null with == here is infinite recursion.
        // Use ReferenceEquals(entry, null) internally. Callers outside the hierarchy are fine.

        public static bool operator ==(TypeEntry left, TypeEntry right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(TypeEntry left, TypeEntry right)
        {
            return !(left == right);
        }

        public abstract override bool Equals(object obj);

        public abstract override int GetHashCode();

        #endregion

        #region Helpers

        protected static readonly TypeEntry[] EmptyEntries = new TypeEntry[0];

        /// <summary>
        /// Whether this entry stands for the given host type. Shorthand for the comparison the
        /// compiler makes more often than any other.
        /// </summary>
        public bool Is<T>()
        {
            return this == TypeEntryCache.Of<T>();
        }

        /// <summary>
        /// Whether this entry stands for the given host type.
        /// </summary>
        public bool Is(Type type)
        {
            return this == TypeEntryCache.Of(type);
        }

        /// <summary>
        /// The CLR types that implement a list of entries.
        /// </summary>
        public static Type[] Materialize(TypeEntry[] entries)
        {
            if (entries == null)
                return null;

            var result = new Type[entries.Length];
            for (var idx = 0; idx < entries.Length; idx++)
                result[idx] = entries[idx]?.Materialize();

            return result;
        }

        /// <summary>
        /// The CLR types that implement a sequence of entries.
        /// </summary>
        public static Type[] Materialize(IEnumerable<TypeEntry> entries)
        {
            return entries?.Select(x => x?.Materialize()).ToArray();
        }

        /// <summary>
        /// Walks this type and everything it inherits from, nearest first.
        /// </summary>
        public IEnumerable<TypeEntry> SelfAndBaseTypes()
        {
            var curr = this;
            while (!ReferenceEquals(curr, null))
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
            return left == right;
        }

        /// <summary>
        /// Checks whether every entry in one list stands for the same type as its counterpart.
        /// </summary>
        public static bool SameAll(TypeEntry[] left, TypeEntry[] right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left is null || right is null || left.Length != right.Length)
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
