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

            if (Parent == null && ParentSignature != null)
                Parent = Context.ResolveType(ParentSignature);

            TypeBuilder = Context.MainModule.DefineType(Name, attrs, Parent);

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
        internal MethodEntity ResolveMethod(string name, Type[] args, bool exact = false)
        {
            if (!_methods.TryGetValue(name, out var group))
                throw new KeyNotFoundException();

            var info = ReflectionHelper.ResolveMethodByArgs(
                group,
                m => m.GetArgumentTypes(Context),
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
        internal ConstructorEntity ResolveConstructor(Type[] args)
        {
            var info = ReflectionHelper.ResolveMethodByArgs(
                _constructors,
                c => c.GetArgumentTypes(Context),
                c => false,
                args
            );

            return info.Method;
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