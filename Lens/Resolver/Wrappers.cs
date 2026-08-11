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
    /// Base class for method or constructor wrappers.
    /// </summary>
    internal class CallableWrapperBase : WrapperBase
    {
        public bool IsPartiallyApplied;
        public bool IsPartiallyResolved;
        public bool IsVariadic;
        public TypeEntry[] ArgumentTypes;
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
            IsStatic = info.IsStatic;
            ReturnType = TypeEntryCache.Of(info.ReturnType);

            var args = info.GetParameters();
            ArgumentTypes = args.Select(p => TypeEntryCache.Of(p.ParameterType)).ToArray();
            IsVariadic = args.Length > 0 && args[args.Length - 1].IsDefined(typeof(ParamArrayAttribute), true);
        }

        public MethodInfo MethodInfo;

        public bool IsVirtual;

        public TypeEntry ReturnType;
        public TypeEntry[] GenericArguments;

        public bool IsGeneric => GenericArguments != null;
    }

    /// <summary>
    /// Wrapper for a constructor entity.
    /// </summary>
    internal class ConstructorWrapper : CallableWrapperBase
    {
        public ConstructorInfo ConstructorInfo;
    }


    /// <summary>
    /// Wrapper for a field entity.
    /// </summary>
    internal class FieldWrapper : WrapperBase
    {
        public FieldInfo FieldInfo;

        public bool IsLiteral;

        public TypeEntry FieldType;
    }

    /// <summary>
    /// Wrapper for a property entity.
    /// </summary>
    internal class PropertyWrapper : WrapperBase
    {
        public TypeEntry PropertyType;
        public MethodInfo Getter;
        public MethodInfo Setter;
        public bool IsVirtual;

        public bool CanGet => Getter != null;
        public bool CanSet => Setter != null;
    }

    /// <summary>
    /// Wrapper for an event entity.
    /// </summary>
    internal class EventWrapper : WrapperBase
    {
        public TypeEntry EventHandlerType;

        public MethodInfo AddMethod;
        public MethodInfo RemoveMethod;
    }
}
