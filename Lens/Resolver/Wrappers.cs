using System;
using System.Linq;
using System.Reflection;

namespace Lens.Resolver
{
    /// <summary>
    /// Base class for all entity wrappers.
    /// </summary>
    internal class WrapperBase
    {
        public string Name;

        /// <summary>
        /// The type that declares the member.
        /// </summary>
        public TypeEntry DeclaringType;

        public bool IsStatic;
    }

    /// <summary>
    /// A reflection object a wrapper hands out on demand.
    ///
    /// A wrapper is resolved structurally: its signature is made of <see cref="TypeEntry"/> values,
    /// which exist whether or not anything has been emitted. The reflection object is the one part
    /// that cannot exist before emission - a member of a declared type, or of a host generic
    /// instantiated over one, has no MethodInfo or FieldInfo until there is an assembly - so the
    /// wrapper holds whatever it was built from and produces the reflection object only when asked,
    /// exactly the way <see cref="TypeEntry.Materialize"/> produces a System.Type.
    ///
    /// Emission asks; analysis never does.
    /// </summary>
    /// <typeparam name="T">Type of the reflection object.</typeparam>
    internal class LazyMember<T> where T : class
    {
        #region Fields

        private Func<T> _source;
        private T _value;
        private bool _resolved;

        #endregion

        #region Members

        /// <summary>
        /// The function that produces the reflection object, called at most once.
        /// </summary>
        public Func<T> Source
        {
            set
            {
                _source = value;
                _resolved = false;
                _value = null;
            }
        }

        /// <summary>
        /// The reflection object itself. Only emission may ask for one.
        /// </summary>
        public T Value
        {
            get
            {
                if (!_resolved)
                {
                    _resolved = true;
                    _value = _source?.Invoke();
                }

                return _value;
            }
            set
            {
                _source = null;
                _resolved = true;
                _value = value;
            }
        }

        #endregion
    }

    /// <summary>
    /// Base class for method or constructor wrappers.
    /// </summary>
    internal class CallableWrapperBase : WrapperBase
    {
        public bool IsPartiallyApplied;
        public bool IsPartiallyResolved;
        public bool IsVariadic;
        public TypeEntry[] ArgumentTypes;

        /// <summary>
        /// The trailing parameters the call site left out, in declaration order, each with the
        /// value its default says. Null when the call spells every argument, which is every call
        /// to anything the script itself declares.
        /// </summary>
        public OmittedArgument[] OmittedArguments;
    }

    /// <summary>
    /// A parameter the call site left out, and what the callee declared to be passed for it.
    /// </summary>
    internal class OmittedArgument
    {
        public OmittedArgument(object value, TypeEntry type)
        {
            Value = value;
            Type = type;
        }

        /// <summary>
        /// The default value, as metadata records it. Null stands for the type's own default.
        /// </summary>
        public readonly object Value;

        /// <summary>
        /// The type of the parameter it is passed to.
        /// </summary>
        public readonly TypeEntry Type;
    }

    /// <summary>
    /// Wrapper for a method entity.
    /// </summary>
    internal class MethodWrapper : CallableWrapperBase
    {
        public MethodWrapper()
        {
        }

        public MethodWrapper(MethodInfo info)
        {
            Name = info.Name;
            DeclaringType = TypeEntryCache.Of(info.DeclaringType);

            MethodInfo = info;
            IsVirtual = info.IsVirtual;
            IsAbstract = info.IsAbstract;
            IsStatic = info.IsStatic;
            ReturnType = TypeEntryCache.Of(info.ReturnType);

            var args = info.GetParameters();
            ArgumentTypes = args.Select(p => TypeEntryCache.Of(p.ParameterType)).ToArray();
            IsVariadic = args.Length > 0 && args[args.Length - 1].IsDefined(typeof(ParamArrayAttribute), true);
        }

        private readonly LazyMember<MethodInfo> _methodInfo = new LazyMember<MethodInfo>();

        /// <summary>
        /// Produces the MethodInfo when emission asks for one.
        /// </summary>
        public Func<MethodInfo> MethodInfoSource
        {
            set => _methodInfo.Source = value;
        }

        public MethodInfo MethodInfo
        {
            get => _methodInfo.Value;
            set => _methodInfo.Value = value;
        }

        public bool IsVirtual;

        /// <summary>
        /// Whether the method is a declaration with no body of its own: an interface member other than
        /// a default implementation, or an abstract member of a class. What tells the two interface
        /// members apart matters to <see cref="Compiler.Context.ResolveInterfaceMethod"/>.
        /// </summary>
        public bool IsAbstract;

        /// <summary>
        /// The generic parameter the method was reached through, when the call to it needs a
        /// 'constrained. !T' prefix: every instance member of a constraint, whose receiver is the
        /// parameter's address, and a static member of an interface constraint, which has no
        /// receiver to say whose implementation is meant. Null for every other call.
        /// </summary>
        public TypeEntry ConstrainedTo;

        public TypeEntry ReturnType;
        public TypeEntry[] GenericArguments;

        public bool IsGeneric => GenericArguments != null;
    }

    /// <summary>
    /// Wrapper for a constructor entity.
    /// </summary>
    internal class ConstructorWrapper : CallableWrapperBase
    {
        private readonly LazyMember<ConstructorInfo> _constructorInfo = new LazyMember<ConstructorInfo>();

        /// <summary>
        /// Produces the ConstructorInfo when emission asks for one.
        /// </summary>
        public Func<ConstructorInfo> ConstructorInfoSource
        {
            set => _constructorInfo.Source = value;
        }

        public ConstructorInfo ConstructorInfo
        {
            get => _constructorInfo.Value;
            set => _constructorInfo.Value = value;
        }
    }


    /// <summary>
    /// Wrapper for a field entity.
    /// </summary>
    internal class FieldWrapper : WrapperBase
    {
        private readonly LazyMember<FieldInfo> _fieldInfo = new LazyMember<FieldInfo>();

        /// <summary>
        /// Produces the FieldInfo when emission asks for one.
        /// </summary>
        public Func<FieldInfo> FieldInfoSource
        {
            set => _fieldInfo.Source = value;
        }

        public FieldInfo FieldInfo
        {
            get => _fieldInfo.Value;
            set => _fieldInfo.Value = value;
        }

        public bool IsLiteral;

        public TypeEntry FieldType;
    }

    /// <summary>
    /// Wrapper for a property entity.
    /// </summary>
    internal class PropertyWrapper : WrapperBase
    {
        public TypeEntry PropertyType;

        /// <summary>
        /// The generic parameter the property was reached through, when its accessors need a
        /// 'constrained. !T' prefix - exactly as <see cref="MethodWrapper.ConstrainedTo"/> does for
        /// a call; null for every other access.
        /// </summary>
        public TypeEntry ConstrainedTo;

        private readonly LazyMember<MethodInfo> _getter = new LazyMember<MethodInfo>();
        private readonly LazyMember<MethodInfo> _setter = new LazyMember<MethodInfo>();

        /// <summary>
        /// Produces the getter when emission asks for one.
        /// </summary>
        public Func<MethodInfo> GetterSource
        {
            set => _getter.Source = value;
        }

        /// <summary>
        /// Produces the setter when emission asks for one.
        /// </summary>
        public Func<MethodInfo> SetterSource
        {
            set => _setter.Source = value;
        }

        public MethodInfo Getter
        {
            get => _getter.Value;
            set => _getter.Value = value;
        }

        public MethodInfo Setter
        {
            get => _setter.Value;
            set => _setter.Value = value;
        }

        public bool IsVirtual;

        // whether the property has an accessor is part of its structure, not of the assembly: the
        // question is answered while binding, when no accessor can be produced yet
        public bool CanGet;
        public bool CanSet;
    }

    /// <summary>
    /// Wrapper for an event entity.
    /// </summary>
    internal class EventWrapper : WrapperBase
    {
        public TypeEntry EventHandlerType;

        /// <summary>
        /// The generic parameter the event was reached through, when its accessors need a
        /// 'constrained. !T' prefix - exactly as <see cref="MethodWrapper.ConstrainedTo"/> does for
        /// a call; null for every other subscription.
        /// </summary>
        public TypeEntry ConstrainedTo;

        private readonly LazyMember<MethodInfo> _addMethod = new LazyMember<MethodInfo>();
        private readonly LazyMember<MethodInfo> _removeMethod = new LazyMember<MethodInfo>();

        /// <summary>
        /// Produces the subscription method when emission asks for one.
        /// </summary>
        public Func<MethodInfo> AddMethodSource
        {
            set => _addMethod.Source = value;
        }

        /// <summary>
        /// Produces the unsubscription method when emission asks for one.
        /// </summary>
        public Func<MethodInfo> RemoveMethodSource
        {
            set => _removeMethod.Source = value;
        }

        public MethodInfo AddMethod
        {
            get => _addMethod.Value;
            set => _addMethod.Value = value;
        }

        public MethodInfo RemoveMethod
        {
            get => _removeMethod.Value;
            set => _removeMethod.Value = value;
        }
    }
}
