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
		public Type Type;

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
		public Type[] ArgumentTypes;
	}

	/// <summary>
	/// Wrapper for a method entity.
	/// </summary>
	internal class MethodWrapper : CallableWrapperBase
	{
		public MethodWrapper() { }

		public MethodWrapper(MethodInfo info)
		{
			Name = info.Name;
			Type = info.DeclaringType;

			MethodInfo = info;
			IsVirtual = info.IsVirtual;
			IsStatic = info.IsStatic;
			ReturnType = info.ReturnType;

			var args = info.GetParameters();
			ArgumentTypes = args.Select(p => p.ParameterType).ToArray();
			IsVariadic = args.Length > 0 && args[args.Length - 1].IsDefined(typeof (ParamArrayAttribute), true);
		}

		public MethodInfo MethodInfo;

		public bool IsVirtual;
		public bool IsStatic;

		public Type ReturnType;
		public Type[] GenericArguments;

		public bool IsGeneric
		{
			get { return GenericArguments != null; }
		}
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

		public Type FieldType;
	}

	/// <summary>
	/// Wrapper for a property entity.
	/// </summary>
	internal class PropertyWrapper : WrapperBase
	{
		public Type PropertyType;
		public MethodInfo Getter;
		public MethodInfo Setter;

		public bool CanGet { get { return Getter != null; } }
		public bool CanSet { get { return Setter != null; } }
	}

	/// <summary>
	/// Wrapper for an event entity.
	/// </summary>
	internal class EventWrapper : WrapperBase
	{
		public Type EventHandlerType;

		public MethodInfo AddMethod;
		public MethodInfo RemoveMethod;
	}
}
