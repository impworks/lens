﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Translations;

namespace Lens.Compiler
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

			_TypePostfixes = new Dictionary<string, Type>
			{
				{ "?", typeof(Nullable<>) },
				{ "~", typeof(IEnumerable<>) }
			};

			loadAssemblies();
		}

		public TypeResolver(Dictionary<string, bool> namespaces)
		{
			_Cache = new Dictionary<string, Type>();
			_Namespaces = namespaces;
		}

		private static IEnumerable<string> _EmptyNamespaces = new[] { string.Empty };
		private static Dictionary<string, List<string>> _Locations;
		private static List<Assembly> _Assemblies;
		private static readonly Dictionary<string, Type> _TypeAliases;
		private static readonly Dictionary<string, Type> _TypePostfixes;

		private readonly Dictionary<string, Type> _Cache;
		private Dictionary<string, bool> _Namespaces;

		/// <summary>
		/// The method that allows external types to be looked up.
		/// </summary>
		public Func<string, Type> ExternalLookup { get; set; }

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
		public Type ResolveType(string signature)
		{
			var trimmed = signature.Replace(" ", string.Empty);
			Type cached;
			if (_Cache.TryGetValue(trimmed, out cached))
				return cached;

			var type = parseTypeSignature(trimmed);
			if (type != null)
				_Cache.Add(trimmed, type);

			return type;
		}

		/// <summary>
		/// Parses the type signature.
		/// </summary>
		private Type parseTypeSignature(string signature)
		{
			// simple cases: type is an alias
			if (_TypeAliases.ContainsKey(signature))
				return _TypeAliases[signature];

			// postfixes
			if (signature.EndsWith("[]"))
				return parseTypeSignature(signature.Substring(0, signature.Length - 2)).MakeArrayType();

			foreach(var postfix in _TypePostfixes)
			{
				if (!signature.EndsWith(postfix.Key))
					continue;

				var bare = parseTypeSignature(signature.Substring(0, signature.Length - postfix.Key.Length));
				return GenericHelper.MakeGenericTypeChecked(postfix.Value, new[] {bare});
			}

			// generic type
			var open = signature.IndexOf('<');
			if (open == -1)
				return findType(signature);

			var close = signature.LastIndexOf('>');
			var args = parseTypeArgs(signature.Substring(open + 1, close - open - 1)).ToArray();
			var typeName = signature.Substring(0, open) + '`' + args.Length;
			var type = findType(typeName);
			return GenericHelper.MakeGenericTypeChecked(type, args);
		}

		/// <summary>
		/// Searches for the specified type in the namespaces.
		/// </summary>
		private Type findType(string name)
		{
			var checkNamespaces = !name.Contains('.');

			if (checkNamespaces && ExternalLookup != null)
			{
				var candidate = ExternalLookup(name);
				if (candidate != null)
					return candidate;
			}
			
			Type foundType = null;

			foreach (var currAsm in _Assemblies)
			{
				var namespaces = checkNamespaces ? _Namespaces.Keys : _EmptyNamespaces;
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
								CompilerMessages.TypeIsAmbiguous,
								name,
								foundType.Namespace,
								foundType.Assembly.GetName().Name,
								type.Namespace,
								currAsm.FullName,
								Environment.NewLine
							)
						);
					}

					foundType = type;
				}
			}

			if (foundType == null)
				throw new ArgumentException(string.Format(CompilerMessages.TypeNotFound, name));

			return foundType;
		}

		/// <summary>
		/// Parses out the list of generic type arguments delimited by commas.
		/// </summary>
		private IEnumerable<Type> parseTypeArgs(string args)
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
					yield return parseTypeSignature(args.Substring(start, idx - start));
					start = idx + 1;
				}
			}

			yield return parseTypeSignature(args.Substring(start, args.Length - start));
		}
	}
}