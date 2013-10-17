using System;
using System.Linq;
using System.Reflection;

namespace Lens.Compiler
{
	public class MethodWrapper
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
			ArgumentTypes = info.GetParameters().Select(p => p.ParameterType).ToArray();
		}

		public string Name;
		public Type Type;
		public bool IsVirtual;
		public bool IsStatic;
		public MethodInfo MethodInfo;
		public Type ReturnType;
		public Type[] ArgumentTypes;
		public Type[] GenericArguments;

		public bool IsGeneric
		{
			get { return GenericArguments != null; }
		}
	}

	public class ConstructorWrapper
	{
		public ConstructorInfo ConstructorInfo;
		public Type Type;
		public Type[] ArgumentTypes;
	}

	public class FieldWrapper
	{
		public string Name;
		public FieldInfo FieldInfo;
		public bool IsStatic;
		public bool IsLiteral;
		public Type Type;
		public Type FieldType;
	}

	public class PropertyWrapper
	{
		public string Name;
		public Type Type;
		public bool IsStatic;
		public Type PropertyType;
		public MethodInfo Getter;
		public MethodInfo Setter;

		public bool CanGet { get { return Getter != null; } }
		public bool CanSet { get { return Setter != null; } }
	}
}
