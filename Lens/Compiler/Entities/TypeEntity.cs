using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.Resolver;

namespace Lens.Compiler.Entities
{
    /// <summary>
    /// Represents a type to be defined in the generated assembly.
    /// </summary>
    internal partial class TypeEntity
    {
        #region Constructor

        public TypeEntity(Context ctx)
        {
            Context = ctx;

            _fields = new Dictionary<string, FieldEntity>();
            _methods = new Dictionary<string, List<MethodEntity>>();
            _constructors = new List<ConstructorEntity>();
        }

        #endregion

        #region Properties

        public TypeEntry[] Interfaces;

        /// <summary>
        /// The interfaces the type declares, before they have been resolved.
        ///
        /// A compiler-generated type is declared long before anything can be resolved - a state
        /// machine is built out of the parse tree - and the interfaces it implements are spelled in
        /// terms of the function's own return type.
        /// </summary>
        public TypeSignature[] InterfaceSignatures;

        private readonly Dictionary<string, FieldEntity> _fields;
        private readonly Dictionary<string, List<MethodEntity>> _methods;
        private readonly List<ConstructorEntity> _constructors;

        /// <summary>
        /// Is true for classes that have been imported from the outer world by compiler configuration.
        /// </summary>
        public bool IsImported => Kind == TypeEntityKind.Imported;

        /// <summary>
        /// Is true for types defined in the script (type, label, record).
        /// </summary>
        public bool IsUserDefined => Kind == TypeEntityKind.Type || Kind == TypeEntityKind.TypeLabel || Kind == TypeEntityKind.Record;

        /// <summary>
        /// Pointer to context.
        /// </summary>
        public Context Context { get; private set; }

        /// <summary>
        /// Checks if the type cannot be inherited from.
        /// </summary>
        public bool IsSealed;

        /// <summary>
        /// Type name.
        /// </summary>
        public string Name;

        /// <summary>
        /// The generic parameters of the type, or null if the type is not generic.
        /// </summary>
        public List<GenericParameterEntity> GenericParameters;

        /// <summary>
        /// The number of generic parameters the type declares.
        /// </summary>
        public int GenericParameterCount => GenericParameters?.Count ?? 0;

        /// <summary>
        /// Checks if the type declares generic parameters.
        /// </summary>
        public bool IsGeneric => GenericParameterCount > 0;

        /// <summary>
        /// The name under which the type is emitted. The CLR requires generic type names
        /// to carry their arity, while LENS keeps referring to the type by its plain name.
        /// </summary>
        public string MangledName => IsGeneric ? Name + "`" + GenericParameterCount : Name;

        /// <summary>
        /// A signature for parent type that might be declared later.
        /// </summary>
        public TypeSignature ParentSignature;

        /// <summary>
        /// The resolved parent type.
        /// </summary>
        public TypeEntry Parent;

        private TypeEntry _typeInfo;

        private TypeEntityEntry _declaredEntry;

        /// <summary>
        /// Whether the declaration has already been resolved. The analysis half runs at most once,
        /// however many times preparation is asked for.
        /// </summary>
        private bool _isResolved;

        /// <summary>
        /// The type as the rest of the compiler refers to it.
        ///
        /// For a declaration this is an entry that answers from the declaration itself, so that
        /// binding never has to ask a half-built TypeBuilder a question it cannot answer. For an
        /// imported type it is whatever the host handed over.
        /// </summary>
        public TypeEntry TypeInfo
        {
            get
            {
                if (IsImported)
                    return _typeInfo;

                return _declaredEntry ?? (_declaredEntry = new TypeEntityEntry(this));
            }
            set
            {
                if (!IsImported)
                    throw new LensCompilerException($"Type '{Name}' is not imported!");

                _typeInfo = value;
            }
        }

        /// <summary>
        /// Produces the CLR type for this declaration, creating its builder if that has not happened
        /// yet. This is the point at which a declaration stops being an idea and starts being
        /// assembly metadata; only emission should reach it.
        /// </summary>
        internal Type MaterializeSelf()
        {
            if (TypeBuilder == null)
                PrepareSelf();

            return TypeBuilder;
        }

        /// <summary>
        /// The typebuilder for current type.
        /// </summary>
        public TypeBuilder TypeBuilder { get; private set; }

        private TypeEntry _selfType;

        /// <summary>
        /// The type as it must be spelled in a signature that refers to this very type:
        /// for a generic type this is the definition constructed over its own parameters
        /// (KeyValue&lt;K, V&gt;), because open generic types cannot appear in metadata.
        /// </summary>
        public TypeEntry SelfType => _selfType ?? TypeInfo;

        /// <summary>
        /// A kind of LENS type this entity represents.
        /// </summary>
        public TypeEntityKind Kind;

        #endregion

        #region Preparation & Compilation

        /// <summary>
        /// Resolves everything the declaration itself states: the constraint model of its generic
        /// parameters and its parent type.
        ///
        /// A generic declaration used to have to wait for its parameter builders, because the parent
        /// of a label is spelled Foo&lt;T&gt; and that was resolved into a constructed CLR type. Now
        /// that a signature resolves into an entry, nothing here needs an assembly.
        /// </summary>
        public void ResolveSelf()
        {
            if (IsImported || _isResolved)
                return;

            if (IsGeneric)
            {
                _isResolved = true;

                Context.RegisterGenericParameters(GenericParameters);
                Context.WithGenericScope(GenericParameters, ResolveParent);
            }
            else
            {
                _isResolved = true;

                ResolveParent();
            }
        }

        /// <summary>
        /// Resolves the parent type from its signature, unless it is known already.
        /// </summary>
        private void ResolveParent()
        {
            if (Parent == null && ParentSignature != null)
                Parent = Context.ResolveType(ParentSignature);

            if (Interfaces == null && InterfaceSignatures != null)
                Interfaces = InterfaceSignatures.Select(x => Context.ResolveType(x)).ToArray();
        }

        /// <summary>
        /// Generates a TypeBuilder for current type entity.
        /// </summary>
        public void PrepareSelf()
        {
            ResolveSelf();
            EmitSelf();
        }

        /// <summary>
        /// Prepares the type as far as the current compilation goes: the declaration always, the
        /// builders only when there is somewhere to emit them into.
        /// </summary>
        public void PrepareSelfAsNeeded()
        {
            ResolveSelf();

            if (Context.IsEmitting)
                EmitSelf();
        }

        /// <summary>
        /// Generates a TypeBuilder for current type entity.
        /// </summary>
        public void EmitSelf()
        {
            if (TypeBuilder != null || IsImported)
                return;

            var attrs = TypeAttributes.Public;
            if (IsSealed)
                attrs |= TypeAttributes.Sealed;

            // a generic type's parent may be expressed in terms of the type's own parameters,
            // so the parameters must exist before the parent is resolved:
            // DefineType -> DefineGenericParameters -> constraints -> SetParent
            if (IsGeneric)
            {
                TypeBuilder = Context.MainModule.DefineType(MangledName, attrs);

                var builders = TypeBuilder.DefineGenericParameters(GenericParameters.Select(p => p.Name).ToArray());
                for (var idx = 0; idx < builders.Length; idx++)
                    GenericParameters[idx].Builder = builders[idx];

                _selfType = TypeEntryCache.Of(Context.Resolver.MakeGenericType(TypeBuilder, builders.Cast<Type>().ToArray()));

                _isResolved = true;

                Context.RegisterGenericParameters(GenericParameters);
                Context.EmitGenericParameters(GenericParameters);

                Context.WithGenericScope(GenericParameters, ResolveParent);

                if (Parent != null)
                    TypeBuilder.SetParent(Parent.Materialize());
            }
            else
            {
                ResolveSelf();

                TypeBuilder = Context.MainModule.DefineType(Name, attrs, Parent?.Materialize());
            }

            if (Interfaces != null)
                foreach (var iface in Interfaces)
                    TypeBuilder.AddInterfaceImplementation(iface.Materialize());

            // a builder that arrives back from reflection - as the definition of an instantiation,
            // say - must resolve to this declaration and not to a bare wrapper around the builder,
            // or the two would be different entries for the same type
            TypeEntryCache.Register(TypeBuilder, TypeInfo);
        }

        /// <summary>
        /// Compile the method bodies of the current class.
        /// </summary>
        public void Compile()
        {
            Context.CurrentType = this;

            foreach (var curr in _constructors)
                if (!curr.IsImported)
                    curr.Compile();

            foreach (var currGroup in _methods)
            foreach (var curr in currGroup.Value)
                if (!curr.IsImported)
                    curr.Compile();
        }

        /// <summary>
        /// Creates auto-generated methods for the type.
        /// </summary>
        public void CreateEntities()
        {
            if (IsUserDefined)
            {
                CreateSpecificEquals();
                CreateGenericEquals();
                CreateGetHashCode();
            }

            if (this == Context.MainType)
            {
                var groups = _methods.ToArray();
                foreach (var currGroup in groups)
                foreach (var currMethod in currGroup.Value)
                    if (currMethod.IsPure)
                        CreatePureWrapper(currMethod);
            }
        }

        #endregion

        #region Structure methods

        /// <summary>
        /// Checks whether the type already declares a field under this name.
        /// </summary>
        internal bool HasField(string name)
        {
            return _fields.ContainsKey(name);
        }

        /// <summary>
        /// Resolves a field assembly entity.
        /// </summary>
        internal FieldEntity ResolveField(string name)
        {
            if (!_fields.TryGetValue(name, out var fe))
                throw new KeyNotFoundException();

            // the builder is emission's business: what a caller needs here is the resolved type of
            // the field, which is what the analysis half of preparation produces
            if (fe.Type == null)
                throw new InvalidOperationException($"Type '{Name}' must be prepared before its entities can be resolved.");

            return fe;
        }

        /// <summary>
        /// Resolves a method assembly entity.
        /// </summary>
        internal MethodEntity ResolveMethod(string name, TypeEntry[] args, bool exact = false, TypeEntry instantiation = null)
        {
            if (!_methods.TryGetValue(name, out var group))
                throw new KeyNotFoundException();

            var info = ReflectionHelper.ResolveMethodByArgs(
                Context.Resolver,
                group,
                m => Substitute(m.GetArgumentTypes(Context), instantiation),
                m => m.IsVariadic,
                args
            );

            if (exact && info.Distance != 0)
                throw new KeyNotFoundException();

            return info.Method;
        }

        /// <summary>
        /// Resolves a group of methods by their name.
        /// </summary>
        internal MethodEntity[] ResolveMethodGroup(string name)
        {
            if (!_methods.TryGetValue(name, out var group))
                throw new KeyNotFoundException();

            return group.ToArray();
        }

        /// <summary>
        /// Resolves a method assembly entity.
        /// </summary>
        internal ConstructorEntity ResolveConstructor(TypeEntry[] args, TypeEntry instantiation = null)
        {
            var info = ReflectionHelper.ResolveMethodByArgs(
                Context.Resolver,
                _constructors,
                c => Substitute(c.GetArgumentTypes(Context), instantiation),
                c => false,
                args
            );

            return info.Method;
        }

        /// <summary>
        /// Rewrites the declared signature of a member in terms of the actual type arguments,
        /// so that overload resolution compares like with like.
        /// </summary>
        private TypeEntry[] Substitute(TypeEntry[] types, TypeEntry instantiation)
        {
            if (instantiation == null)
                return types;

            // the declaration's own parameters, not the ones its entry reports: an entity that has
            // not been emitted has no parameter builders, and the substitution has to work anyway
            var parameters = GenericParameters.Select(x => x.TypeInfo).ToArray();

            return types
                .Select(x => ConstructedTypeEntry.SubstituteInto(Context.Resolver, x, parameters, instantiation.GenericArguments))
                .ToArray();
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return Name;
        }

        #endregion
    }
}