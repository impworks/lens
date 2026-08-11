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

        public Type[] Interfaces;

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
        public Type Parent;

        private Type _typeInfo;

        public Type TypeInfo
        {
            get => TypeBuilder ?? _typeInfo;
            set
            {
                if (!IsImported)
                    throw new LensCompilerException($"Type '{Name}' is not imported!");

                _typeInfo = value;
            }
        }

        /// <summary>
        /// The typebuilder for current type.
        /// </summary>
        public TypeBuilder TypeBuilder { get; private set; }

        private Type _selfType;

        /// <summary>
        /// The type as it must be spelled in a signature that refers to this very type:
        /// for a generic type this is the definition constructed over its own parameters
        /// (KeyValue&lt;K, V&gt;), because open generic types cannot appear in metadata.
        /// </summary>
        public Type SelfType => _selfType ?? TypeInfo;

        /// <summary>
        /// A kind of LENS type this entity represents.
        /// </summary>
        public TypeEntityKind Kind;

        #endregion

        #region Preparation & Compilation

        /// <summary>
        /// Generates a TypeBuilder for current type entity.
        /// </summary>
        public void PrepareSelf()
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

                _selfType = Context.Resolver.MakeGenericType(TypeBuilder, builders.Cast<Type>().ToArray());

                Context.ResolveGenericParameters(GenericParameters);

                Context.WithGenericScope(GenericParameters, () =>
                    {
                        if (Parent == null && ParentSignature != null)
                            Parent = Context.ResolveType(ParentSignature);
                    }
                );

                if (Parent != null)
                    TypeBuilder.SetParent(Parent);
            }
            else
            {
                if (Parent == null && ParentSignature != null)
                    Parent = Context.ResolveType(ParentSignature);

                TypeBuilder = Context.MainModule.DefineType(Name, attrs, Parent);
            }

            if (Interfaces != null)
                foreach (var iface in Interfaces)
                    TypeBuilder.AddInterfaceImplementation(iface);
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
        /// Resolves a field assembly entity.
        /// </summary>
        internal FieldEntity ResolveField(string name)
        {
            if (!_fields.TryGetValue(name, out var fe))
                throw new KeyNotFoundException();

            if (fe.FieldBuilder == null)
                throw new InvalidOperationException($"Type '{Name}' must be prepared before its entities can be resolved.");

            return fe;
        }

        /// <summary>
        /// Resolves a method assembly entity.
        /// </summary>
        internal MethodEntity ResolveMethod(string name, Type[] args, bool exact = false, Type instantiation = null)
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
        internal ConstructorEntity ResolveConstructor(Type[] args, Type instantiation = null)
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
        private static Type[] Substitute(Type[] types, Type instantiation)
        {
            if (instantiation == null)
                return types;

            return types.Select(x => GenericHelper.ApplyGenericArguments(x, instantiation, false)).ToArray();
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