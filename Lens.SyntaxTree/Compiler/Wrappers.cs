using System;
using System.Reflection;

namespace Lens.SyntaxTree.Compiler
{
	public class MethodWrapper
	{
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
