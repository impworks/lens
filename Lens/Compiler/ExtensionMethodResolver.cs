using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Lens.Utils;

namespace Lens.Compiler
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
		private Dictionary<string, bool> _Namespaces;

		/// <summary>
		/// Gets an extension method by given arguments.
		/// </summary>
		public MethodInfo FindExtensionMethod(Type type, string name, Type[] args )
		{
			if (!_Cache.ContainsKey(type))
				findMethodsForType(type);

			if(!_Cache[type].ContainsKey(name))
				throw new KeyNotFoundException();

			var methods = _Cache[type][name];
			var result = methods.Where(m => m.Name == name)
								.Select(mi => new { Method = mi, Distance = GetExtensionDistance(mi, type, args) })
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

		public static int GetExtensionDistance(MethodInfo method, Type type, Type[] args)
		{
			var methodArgs = method.GetParameters().Select(p => p.ParameterType);
			var baseDist = methodArgs.First().DistanceFrom(type);
			var argsDist = GetArgumentsDistance(methodArgs.Skip(1), args);

			try
			{
				return checked(baseDist + argsDist);
			}
			catch (OverflowException)
			{
				return int.MaxValue;
			}
		}

		/// <summary>
		/// Gets total distance between two sets of argument types.
		/// </summary>
		public static int GetArgumentsDistance(IEnumerable<Type> src, IEnumerable<Type> dst)
		{
			var srcIt = src.GetEnumerator();
			var dstIt = dst.GetEnumerator();

			var totalDist = 0;
			while (true)
			{
				var srcOk = srcIt.MoveNext();
				var dstOk = dstIt.MoveNext();

				if (srcOk != dstOk)
					return int.MaxValue;

				if (srcOk == false)
					return totalDist;

				var dist = dstIt.Current.DistanceFrom(srcIt.Current);
				if (dist == int.MaxValue)
					return int.MaxValue;

				totalDist += dist;
			}
		}
	}
}
