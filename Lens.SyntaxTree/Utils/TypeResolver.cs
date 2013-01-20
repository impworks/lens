using System;
using System.Collections.Generic;
using System.Linq;

namespace Lens.SyntaxTree.Utils
{
	/// <summary>
	/// A class to resolve types by their string signatures.
	/// </summary>
	public class TypeResolver
	{
		public TypeResolver()
		{
			_Cache = new Dictionary<string, Type>();
			ResetLocations();
			ResetAliases();
		}

		private Dictionary<string, List<string>> _SearchableLocations;
		private Dictionary<string, Type> _Cache;

		/// <summary>
		/// Type aliases.
		/// </summary>
		public Dictionary<string, Type> TypeAliases { get; private set; }


		/// <summary>
		/// Initialize the namespaces list.
		/// </summary>
		public void ResetLocations()
		{
			_SearchableLocations = new Dictionary<string, List<string>>
			{
				{
					// Local reference
					"",
					new List<string>()
				},
				{
					"mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
					new List<string>
					{
						"System",
						"System.Collections.Generic",
						"System.Text"
					}
				},
				{
					"System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
					new List<string>
					{
						"System",
						"System.Text.RegularExpressions"
					}
				}
			};
		}

		/// <summary>
		/// Initialize the aliases dictionary.
		/// </summary>
		public void ResetAliases()
		{
			TypeAliases = new Dictionary<string, Type>
			{
				{"object", typeof (Object)},
				{"bool", typeof (Boolean)},
				{"int", typeof (Int32)},
				{"long", typeof (Int64)},
				{"float", typeof (Single)},
				{"double", typeof (Double)},
				{"string", typeof (String)},
			};
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
			if(type != null)
				_Cache.Add(trimmed, type);

			return type;
		}

		/// <summary>
		/// Add a namespace to search types in.
		/// </summary>
		/// <param name="nsp">Namespace.</param>
		/// <param name="assembly">Optional assembly full name.</param>
		public void AddNamespace(string nsp, string assembly = "")
		{
			if (_SearchableLocations.ContainsKey(assembly))
			{
				var lst = _SearchableLocations[assembly];
				if (!lst.Contains(nsp))
					lst.Add(nsp);

				return;
			}

			_SearchableLocations[assembly] = new List<string> { nsp };
		}

		/// <summary>
		/// Parses the type signature.
		/// </summary>
		private Type parseTypeSignature(string signature)
		{
			// simple cases: type is an alias
			if (TypeAliases.ContainsKey(signature))
				return TypeAliases[signature];

			// array
			if (signature.EndsWith("[]"))
				return parseTypeSignature(signature.Substring(0, signature.Length - 2)).MakeArrayType();

			// generic type
			var open = signature.IndexOf('<');
			var close = signature.LastIndexOf('>');

			if (open == -1)
				return findType(signature);

			var args = parseTypeArgs(signature.Substring(open + 1, close - open - 1)).ToArray();
			var typeName = signature.Substring(0, open) + '`' + args.Length;
			var type = findType(typeName);
			return type.MakeGenericType(args);
		}

		/// <summary>
		/// Searches for the specified type in the namespaces.
		/// </summary>
		private Type findType(string name)
		{
			var checkNamespaces = !name.Contains('.');

			Type foundType = null;
			string foundAsm = null;
			string foundNsp = null;

			foreach (var currLocation in _SearchableLocations)
			{
				var nsps = checkNamespaces
					? currLocation.Value
					: (IEnumerable<string>) new[] {string.Empty};

				foreach (var currNsp in nsps)
				{
					var typeName = checkNamespaces ? string.Format("{0}.{1}", currNsp, name) : name;
					if (!string.IsNullOrEmpty(currLocation.Key))
						typeName = string.Format("{0},{1}", typeName, currLocation.Key);

					var type = Type.GetType(typeName);
					if (type == null)
						continue;

					if (foundType != null && foundType != type)
					{
						throw new ArgumentException(
							string.Format(
								"Ambigious type reference: type '{0}' is found in the following namespaces:\n{1} in assembly {2}\n{3} in assembly {4}",
								name,
								foundNsp,
								foundAsm,
								currNsp,
								currLocation.Key
							)
						);
					}

					foundType = type;
					foundNsp = currNsp;
					foundAsm = currLocation.Key;
				}
			}

			if(foundType == null)
				throw new ArgumentException(string.Format("Type '{0}' could not be found.", name));

			return foundType;
		}

		/// <summary>
		/// Parses out the list of generic type arguments delimited by commas.
		/// </summary>
		private IEnumerable<Type> parseTypeArgs(string args)
		{
			var depth = 0;
			var start = 0;
			for (var idx = 0; idx < args.Length; idx++)
			{
				if (args[idx] == '<') depth++;
				if (args[idx] == '>') depth--;
				if (args[idx] == ',' && depth == 0)
				{
					yield return parseTypeSignature(args.Substring(start, idx - start));
					start = idx + 1;
				}
			}

			yield return parseTypeSignature(args.Substring(start, args.Length - start));
		}
	}
}
