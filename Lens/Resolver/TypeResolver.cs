using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Translations;

namespace Lens.Resolver
{
	/// <summary>
	/// A class to resolve types by their string signatures.
	/// </summary>
	internal class TypeResolver
	{
		#region Constructors

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
					"System.Core",
					new List<string> { "System.Linq" }
				}
			};

			_TypeAliases = new Dictionary<string, Type>
			{
				{"object", typeof (object)},
				{"bool", typeof (bool)},
				{"int", typeof (int)},
				{"long", typeof (long)},
				{"float", typeof (float)},
				{"double", typeof (double)},
				{"decimal", typeof (decimal)},
				{"string", typeof (string)},
				{"char", typeof (char)},
				{"byte", typeof (byte)},
			};
		}

		public TypeResolver(Dictionary<string, bool> namespaces, ReferencedAssemblyCache asmCache)
		{
			_Cache = new Dictionary<string, Type>();
			_Namespaces = namespaces;
			_AsmCache = asmCache;
		}

		#endregion

		#region Fields

		/// <summary>
		/// List of known locations: assembly name and the list of default namespaces in it.
		/// </summary>
		private static Dictionary<string, List<string>> _Locations;

		/// <summary>
		/// List of known type short names (like 'int' = 'System.Int32').
		/// </summary>
		private static readonly Dictionary<string, Type> _TypeAliases;

		/// <summary>
		/// Cached list of already resolved types.
		/// </summary>
		private readonly Dictionary<string, Type> _Cache;

		/// <summary>
		/// List of namespaces to check when finding the type.
		/// </summary>
		private Dictionary<string, bool> _Namespaces;

		/// <summary>
		/// List of referenced assemblies.
		/// </summary>
		private readonly ReferencedAssemblyCache _AsmCache;

		/// <summary>
		/// The method that allows external types to be looked up.
		/// </summary>
		public Func<string, Type> ExternalLookup { get; set; }

		#endregion

		#region Methods

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

		#endregion

		#region Helpers

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
		private static Type processPostfix(Type type, string postfix)
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

			foreach (var currAsm in _AsmCache.Assemblies)
			{
				var namespaces = checkNamespaces ? _Namespaces.Keys : (IEnumerable<string>)new [] { string.Empty };
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

		#endregion
	}
}
