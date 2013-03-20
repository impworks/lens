using System;
using System.Reflection;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// Represents a property imported from external code.
	/// </summary>
	internal class GlobalPropertyEntity
	{
		public readonly Type PropertyType;
		public readonly MethodInfo Setter;
		public readonly MethodInfo Getter;

		public GlobalPropertyEntity(MethodInfo getter, MethodInfo setter)
		{
			if (getter == null && setter == null)
				throw new LensCompilerException("Cannot create a property without getter and setter!");

			var getterType = getter != null ? getter.ReturnType : null;
			var setterType = setter != null ? setter.GetParameters()[0].ParameterType : null;

			if (getterType != null && setterType != null && getterType != setterType)
				throw new LensCompilerException("Getter and setter types do not match!");
			
			PropertyType = getterType ?? setterType;
			Getter = getter;
			Setter = setter;
		}

		public static GlobalPropertyEntity Create<T>(Func<T> getter, Action<T> setter = null)
		{
			return new GlobalPropertyEntity(getter != null ? getter.Method : null, setter != null ? setter.Method : null);
		}

		public static GlobalPropertyEntity Create(Delegate getter, Delegate setter = null)
		{
			return new GlobalPropertyEntity(getter != null ? getter.Method : null, setter != null ? setter.Method : null);
		}
	}
}
