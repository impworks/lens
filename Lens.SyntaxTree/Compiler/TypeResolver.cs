using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// A class to resolve types by their string signatures.
	/// </summary>
	public class TypeResolver
	{
		static TypeResolver()
		{
			_Locations = new Dictionary<string, List<string>>
			{
				{
					"mscorlib",
					new List<string> { "System.Collections", "System.Collections.Generic", "System.Text", "System.Threading" }
				},
				{
					"System",
					new List<string> { "System.Text.RegularExpressions" }
				},
				{
					"System.Drawing",
					new List<string> { "System.Drawing" }
				},
				{
					"System.Core",
					new List<string> { "System.Linq" }
				}
			};

			_Namespaces = new List<string>
			{
				"System"
			};

			_TypeAliases = new Dictionary<string, Type>
			{
				{"object", typeof (Object)},
				{"bool", typeof (Boolean)},
				{"int", typeof (Int32)},
				{"long", typeof (Int64)},
				{"float", typeof (Single)},
				{"double", typeof (Double)},
				{"string", typeof (String)},
			};

			loadAssemblies();
		}

		public TypeResolver()
		{
			_Cache = new Dictionary<string, Type>();
		}

		private static IEnumerable<string> _EmptyNamespaces = new[] { string.Empty };
		private static Dictionary<string, List<string>> _Locations;
		private static List<string> _Namespaces;
		private readonly Dictionary<string, Type> _Cache;
		private static List<Assembly> _Assemblies;
		private static readonly Dictionary<string, Type> _TypeAliases;

		private static void loadAssemblies()
		{
			_Assemblies = new List<Assembly>();
			var fullNames = new[]
			{
				"mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
				"System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
				"System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
				"System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"
			};

			foreach (var name in fullNames)
			{
				try
				{
					_Assemblies.Add(Assembly.Load(name));
				}
				catch { }
			}

			foreach(var asm in AppDomain.CurrentDomain.GetAssemblies())
				if(!_Assemblies.Contains(asm))
					_Assemblies.Add(asm);
		}

		/// <summary>
		/// Resolves a type by its string signature.
		/// </summary>
		public Type ResolveType(string signature, bool allowGeneric = false)
		{
			var trimmed = signature.Replace(" ", string.Empty);
			Type cached;
			if (_Cache.TryGetValue(trimmed, out cached))
				return cached;

			var type = parseTypeSignature(trimmed, allowGeneric);
			if (type != null)
				_Cache.Add(trimmed, type);

			return type;
		}

		/// <summary>
		/// Add a namespace to search types in.
		/// </summary>
		/// <param name="nsp">Namespace.</param>
		public void AddNamespace(string nsp)
		{
			if(!_Namespaces.Contains(nsp))
				_Namespaces.Add(nsp);
		}

		/// <summary>
		/// Parses the type signature.
		/// </summary>
		private Type parseTypeSignature(string signature, bool allowGeneric)
		{
			// simple cases: type is an alias
			if (_TypeAliases.ContainsKey(signature))
				return _TypeAliases[signature];

			// array
			if (signature.EndsWith("[]"))
				return parseTypeSignature(signature.Substring(0, signature.Length - 2), false).MakeArrayType();

			// generic type
			var open = signature.IndexOf('<');
			if (open == -1)
				return findType(signature);

			var close = signature.LastIndexOf('>');
			var args = parseTypeArgs(signature.Substring(open + 1, close - open - 1), allowGeneric).ToArray();
			var typeName = signature.Substring(0, open) + '`' + args.Length;
			var type = findType(typeName);
			return args.Any(x => x == null) ? type : type.MakeGenericType(args);
		}

		/// <summary>
		/// Searches for the specified type in the namespaces.
		/// </summary>
		private Type findType(string name)
		{
			var checkNamespaces = !name.Contains('.');
			
			Type foundType = null;

			foreach (var currAsm in _Assemblies)
			{
				var namespaces = checkNamespaces ? _Namespaces : _EmptyNamespaces;
				if (checkNamespaces)
				{
					List<string> extras;
					if (_Locations.TryGetValue(currAsm.GetName().Name, out extras))
						namespaces = namespaces.Union(extras);
				}

				foreach (var currNsp in namespaces)
				{
					var typeName = (checkNamespaces ? currNsp + "." + name : name)  + "," + currAsm.FullName;
					var type = Type.GetType(typeName);
					if (type == null)
						continue;

					if (foundType != null && foundType != type)
					{
						throw new ArgumentException(
							string.Format(
								"Ambigious type reference: type '{0}' is found in the following namespaces:\n{1} in assembly {2}\n{3} in assembly {4}",
								name,
								foundType.Namespace,
								foundType.Assembly.GetName().Name,
								type.Namespace,
								currAsm.FullName
							)
						);
					}

					foundType = type;
				}
			}

			if (foundType == null)
				throw new ArgumentException(string.Format("Type '{0}' could not be found.", name));

			return foundType;
		}

		/// <summary>
		/// Parses out the list of generic type arguments delimited by commas.
		/// </summary>
		private IEnumerable<Type> parseTypeArgs(string args, bool allowGeneric)
		{
			var depth = 0;
			var start = 0;
			var len = args.Length;
			for (var idx = 0; idx < len; idx++)
			{
				if (args[idx] == '<') depth++;
				if (args[idx] == '>') depth--;
				if (depth == 0 && args[idx] == ',')
				{
					yield return getGenericArgumentType(args.Substring(start, idx - start), allowGeneric);
					start = idx + 1;
				}
			}

			yield return getGenericArgumentType(args.Substring(start, args.Length - start), allowGeneric);
		}

		/// <summary>
		/// Substitutes a null for placeholder type.
		/// </summary>
		private Type getGenericArgumentType(string str, bool allowGeneric)
		{
			return str == "_" && allowGeneric ? null : parseTypeSignature(str, false);
		}
	}
}
