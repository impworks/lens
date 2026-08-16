using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            Locations = new Dictionary<string, List<string>>
            {
                {
                    "mscorlib",
                    new List<string> {"System.Collections", "System.Collections.Generic", "System.Text", "System.Threading"}
                },
                {
                    "System",
                    new List<string> {"System.Text.RegularExpressions"}
                },
                {
                    "System.Core",
                    new List<string> {"System.Linq"}
                }
            };

            TypeAliases = new Dictionary<string, TypeEntry>
            {
                {"object", TypeEntryCache.Of<object>()},
                {"bool", TypeEntryCache.Of<bool>()},
                {"int", TypeEntryCache.Of<int>()},
                {"long", TypeEntryCache.Of<long>()},
                {"float", TypeEntryCache.Of<float>()},
                {"double", TypeEntryCache.Of<double>()},
                {"decimal", TypeEntryCache.Of<decimal>()},
                {"string", TypeEntryCache.Of<string>()},
                {"char", TypeEntryCache.Of<char>()},
                {"byte", TypeEntryCache.Of<byte>()},
            };
        }

        public TypeResolver(TypeResolutionContext resolutionContext, Dictionary<string, bool> namespaces, ReferencedAssemblyCache asmCache)
        {
            _cache = new Dictionary<string, TypeEntry>();
            _resolutionContext = resolutionContext;
            _namespaces = namespaces;
            _asmCache = asmCache;
        }

        #endregion

        #region Fields

        /// <summary>
        /// List of known locations: assembly name and the list of default namespaces in it.
        /// </summary>
        private static readonly Dictionary<string, List<string>> Locations;

        /// <summary>
        /// The namespaces an assembly contributes on top of the ones the script imported, or null
        /// when it contributes none. A type in one of these resolves without a 'use' directive, so
        /// anything listing what a script can name has to look in them too.
        /// </summary>
        public static IEnumerable<string> ImplicitNamespacesOf(Assembly asm)
        {
            return Locations.TryGetValue(asm.GetName().Name, out var result) ? result : null;
        }

        /// <summary>
        /// List of known type short names (like 'int' = 'System.Int32').
        /// </summary>
        private static readonly Dictionary<string, TypeEntry> TypeAliases;

        /// <summary>
        /// The short names the language gives to host types, for anything that has to spell a type
        /// the way a script would have written it.
        /// </summary>
        public static IEnumerable<KeyValuePair<string, TypeEntry>> Aliases => TypeAliases;

        /// <summary>
        /// The resolution context of the current compilation.
        /// </summary>
        private readonly TypeResolutionContext _resolutionContext;

        /// <summary>
        /// Cached list of already resolved types.
        /// </summary>
        private readonly Dictionary<string, TypeEntry> _cache;

        /// <summary>
        /// List of namespaces to check when finding the type.
        /// </summary>
        private readonly Dictionary<string, bool> _namespaces;

        /// <summary>
        /// List of referenced assemblies.
        /// </summary>
        private readonly ReferencedAssemblyCache _asmCache;

        /// <summary>
        /// The method that allows external types to be looked up.
        /// </summary>
        public Func<string, TypeEntry> ExternalLookup { get; set; }

        #endregion

        #region Methods

        /// <summary>
        /// Resolves a type by its string signature.
        /// </summary>
        public TypeEntry ResolveType(TypeSignature signature)
        {
            if (_cache.TryGetValue(signature.FullSignature, out var cached))
                return cached;

            var type = ParseTypeSignature(signature);

            // a type parameter means different things in different declarations and a declaration's
            // shape can still change, so nothing built out of one may be memoized by name
            if (type != null && !type.ContainsDeclared)
                _cache.Add(signature.FullSignature, type);

            return type;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Parses the type signature.
        /// </summary>
        private TypeEntry ParseTypeSignature(TypeSignature signature)
        {
            try
            {
                if (!string.IsNullOrEmpty(signature.Postfix))
                    return ProcessPostfix(ParseTypeSignature(signature.Arguments[0]), signature.Postfix);

                var name = signature.Name;
                var hasArgs = signature.Arguments != null && signature.Arguments.Length > 0;
                if (hasArgs)
                    name += "`" + signature.Arguments.Length;

                if (TypeAliases.ContainsKey(name))
                    return TypeAliases[name];

                var type = FindType(name);
                return hasArgs
                    ? GenericHelper.MakeGenericTypeChecked(_resolutionContext, type, signature.Arguments.Select(ParseTypeSignature).ToArray())
                    : type;
            }
            catch (Exception ex)
            {
                throw new LensCompilerException(ex.Message, signature);
            }
        }

        /// <summary>
        /// Wraps a type into a specific postfix.
        /// </summary>
        private TypeEntry ProcessPostfix(TypeEntry type, string postfix)
        {
            if (postfix == "[]")
                return type.MakeArray(_resolutionContext);

            if (postfix == "~")
                return TypeEntry.Generic(_resolutionContext, typeof(IEnumerable<>), type);

            if (postfix == "?")
            {
                // checked, not MakeNullable: Nullable<T> demands a non-nullable value type, and
                // 'SomeRecord?' has to be rejected with the language's own message rather than
                // accepted here and blamed on the CLR later
                return GenericHelper.MakeGenericTypeChecked(_resolutionContext, TypeEntryCache.Of(typeof(Nullable<>)), type);
            }

            throw new ArgumentException(string.Format("Unknown postfix '{0}'!", postfix));
        }

        /// <summary>
        /// Searches for the specified type in the namespaces.
        /// </summary>
        private TypeEntry FindType(string name)
        {
            var checkNamespaces = !name.Contains('.');

            if (checkNamespaces && ExternalLookup != null)
            {
                var candidate = ExternalLookup(name);
                if (candidate != null)
                    return candidate;
            }

            Type foundType = null;

            foreach (var currAsm in _asmCache.Assemblies)
            {
                var namespaces = checkNamespaces ? _namespaces.Keys : (IEnumerable<string>) new[] {string.Empty};
                if (checkNamespaces)
                {
                    if (Locations.TryGetValue(currAsm.GetName().Name, out List<string> extras))
                        namespaces = namespaces.Union(extras);
                }

                foreach (var currNsp in namespaces)
                {
                    var typeName = (checkNamespaces ? currNsp + "." + name : name) + "," + currAsm.FullName;
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

            return TypeEntryCache.Of(foundType);
        }

        #endregion
    }
}