using System;
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
		public Type ResolveType(TypeSignature signature)
		{
			Type cached;
			if (_Cache.TryGetValue(signature.FullSignature, out cached))
				return cached;

			var type = parseTypeSignature(signature);
			if (type != null)
				_Cache.Add(signature.FullSignature, type);

			return type;
		}

		/// <summary>
		/// Parses the type signature.
		/// </summary>
		private Type parseTypeSignature(TypeSignature signature)
		{
			try
			{
				if (!string.IsNullOrEmpty(signature.Postfix))
					return processPostfix(parseTypeSignature(signature.Arguments[0]), signature.Postfix);

				var name = signature.Name;
				var hasArgs = signature.Arguments != null && signature.Arguments.Length > 0;
				if (hasArgs)
					name += "`" + signature.Arguments.Length;

				if (_TypeAliases.ContainsKey(name))
					return _TypeAliases[name];

				var type = findType(name);
				return hasArgs
					? GenericHelper.MakeGenericTypeChecked(type, signature.Arguments.Select(parseTypeSignature).ToArray())
					: type;
			}
			catch (ArgumentException ex)
			{
				throw new LensCompilerException(ex.Message, signature);
			}
		}

		/// <summary>
		/// Wraps a type into a specific postfix.
		/// </summary>
		private Type processPostfix(Type type, string postfix)
		{
			if (postfix == "[]")
				return type.MakeArrayType();

			if (postfix == "~")
				return GenericHelper.MakeGenericTypeChecked(typeof (IEnumerable<>), type);

			if (postfix == "?")
				return GenericHelper.MakeGenericTypeChecked(typeof(Nullable<>), type);

			throw new ArgumentException(string.Format("Unknown postfix '{0}'!", postfix));
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
	}
}
