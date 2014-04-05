using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Lens.Utils
{
	/// <summary>
	/// A collection is useful tools for working with Reflection entities.
	/// </summary>
	internal static class ReflectionHelper
	{
		/// <summary>
		/// Checks if method can accept an arbitrary amount of arguments.
		/// </summary>
		public static bool IsVariadic(MethodBase method)
		{
			var args = method.GetParameters();
			return args.Length > 0 && args[args.Length - 1].IsDefined(typeof(ParamArrayAttribute), true);
		}

		/// <summary>
		/// Returns the list of methods by name, flattening interface hierarchy.
		/// </summary>
		public static IEnumerable<MethodInfo> GetMethodsByName(Type type, string name)
		{
			const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy;

			var result = type.GetMethods(flags).Where(m => m.Name == name);
			if (type.IsInterface && !result.Any())
				result = type.GetInterfaces().SelectMany(x => GetMethodsByName(x, name));

			return result;
		}

		/// <summary>
		/// Checks if the list of argument types denotes a partial application case.
		/// </summary>
		public static bool IsPartiallyApplied(Type[] argTypes)
		{
			return argTypes.Contains(null);
		}
	}
}
