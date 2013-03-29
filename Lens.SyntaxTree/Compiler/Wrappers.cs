using System;
using System.Reflection;

namespace Lens.SyntaxTree.Compiler
{
	public class MethodWrapper
	{
		public MethodWrapper(string name, MethodInfo info, Type retType, Type[] argTypes, Type[] generics = null)
		{
			Name = name;
			MethodInfo = info;
			ReturnType = retType;
			ArgumentTypes = argTypes;
			GenericArguments = generics;
		}

		public readonly string Name;
		public readonly MethodInfo MethodInfo;
		public readonly Type ReturnType;
		public readonly Type[] ArgumentTypes;
		public readonly Type[] GenericArguments;

		public bool IsGeneric
		{
			get { return GenericArguments != null; }
		}
	}

	public class ConstructorWrapper
	{
		public ConstructorWrapper(ConstructorInfo info, Type type, Type[] argTypes)
		{
			ConstructorInfo = info;
			Type = type;
			ArgumentTypes = argTypes;
		}

		public readonly ConstructorInfo ConstructorInfo;
		public readonly Type Type;
		public readonly Type[] ArgumentTypes;
	}

	public class FieldWrapper
	{
		public FieldWrapper(FieldInfo info, Type type, Type fieldType)
		{
			FieldInfo = info;
			Type = type;
			FieldType = fieldType;
		}

		public readonly FieldInfo FieldInfo;
		public readonly Type Type;
		public readonly Type FieldType;
	}

	public class PropertyWrapper
	{
		public PropertyWrapper(Type type, Type ptyType, MethodInfo getter, MethodInfo setter)
		{
			Type = type;
			PropertyType = ptyType;
			Getter = getter;
			Setter = setter;
		}

		public readonly Type Type;
		public readonly Type PropertyType;
		public readonly MethodInfo Getter;
		public readonly MethodInfo Setter;

		public bool CanGet { get { return Getter != null; } }
		public bool CanSet { get { return Setter != null; } }
	}
}
