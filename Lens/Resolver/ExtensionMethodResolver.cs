using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Lens.Resolver
{
	/// <summary>
	/// Finds a list of possible extension methods for a given type.
	/// </summary>
	internal class ExtensionMethodResolver
	{
		static ExtensionMethodResolver()
		{
			_Cache = new Dictionary<Type, Dictionary<string, List<MethodInfo>>>();
		}

		public ExtensionMethodResolver(Dictionary<string, bool> namespaces)
		{
			_Namespaces = namespaces;
		}

		private static readonly Dictionary<Type, Dictionary<string, List<MethodInfo>>> _Cache;
		private readonly Dictionary<string, bool> _Namespaces;

		/// <summary>
		/// Gets an extension method by given arguments.
		/// </summary>
		public MethodInfo FindExtensionMethod(Type type, string name, Type[] args)
		{
			if (!_Cache.ContainsKey(type))
				findMethodsForType(type);

			if(!_Cache[type].ContainsKey(name))
				throw new KeyNotFoundException();

			var methods = _Cache[type][name];
			var result = methods.Where(m => m.Name == name)
								.Select(mi => new { Method = mi, Distance = getExtensionDistance(mi, type, args) })
								.OrderBy(p => p.Distance)
								.Take(2)
								.ToArray();

			if (result.Length == 0 || result[0].Distance == int.MaxValue)
				throw new KeyNotFoundException();

			if (result.Length > 1 && result[0].Distance == result[1].Distance)
				throw new AmbiguousMatchException();

			return result[0].Method;
		}

		private void findMethodsForType(Type forType)
		{
			var dict = new Dictionary<string, List<MethodInfo>>();

			var asms = AppDomain.CurrentDomain.GetAssemblies();
			foreach (var asm in asms)
			{
				if (asm.IsDynamic)
					continue;

				try
				{
					var types = asm.GetExportedTypes();
					foreach (var type in types)
					{
						if (!type.IsSealed || type.IsGenericType || !type.IsDefined(typeof (ExtensionAttribute), false))
							continue;

						if (type.Namespace == null || !_Namespaces.ContainsKey(type.Namespace))
							continue;

						var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public);
						foreach (var method in methods)
						{
							if (!method.IsDefined(typeof (ExtensionAttribute), false))
								continue;

							var argType = method.GetParameters()[0].ParameterType;
							if (!argType.IsExtendablyAssignableFrom(forType))
								continue;

							if (!dict.ContainsKey(method.Name))
								dict[method.Name] = new List<MethodInfo>();

							dict[method.Name].Add(method);
						}
					}
				}
				catch(Exception ex)
				{
					Debug.WriteLine(ex);
				}
			}

			_Cache[forType] = dict;
		}

		private static int getExtensionDistance(MethodInfo method, Type type, Type[] args)
		{
			var methodArgs = method.GetParameters().Select(p => p.ParameterType).ToArray();
			var baseDist = methodArgs.First().DistanceFrom(type);
			var argsDist = TypeExtensions.TypeListDistance(args, methodArgs.Skip(1));

			if(baseDist == int.MaxValue || argsDist == int.MaxValue)
				return int.MaxValue;

			return baseDist + argsDist;
		}
	}
}
