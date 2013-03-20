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

			if (getter != null)
			{
				if (!getter.IsStatic)
					throw new LensCompilerException("Property getter must be a static method!");

				if(getter.GetParameters().Length > 0)
					throw new LensCompilerException("Property getter must not contain parameters!");

				PropertyType = getter.ReturnType;
			}

			if (setter != null)
			{
				if (!setter.IsStatic)
					throw new LensCompilerException("Property setter must be a static method!");

				if(setter.ReturnType != typeof(void))
					throw new LensCompilerException("Property setter must be a void method!");

				var ps = setter.GetParameters();
				if (ps.Length != 1)
					throw new LensCompilerException("Property setter must have exactly one parameter!");

				if (PropertyType == null)
					PropertyType = ps[0].ParameterType;

				if (ps[0].ParameterType != PropertyType)
					throw new LensCompilerException("Getter and setter types do not match!");
			}

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
