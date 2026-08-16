using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Lens.Resolver
{
    /// <summary>
    /// The internal list of assemblies referenced by the current script.
    /// </summary>
    internal class ReferencedAssemblyCache
    {
        #region Constructor

        public ReferencedAssemblyCache(bool useDefault = true)
        {
            _assemblies = new HashSet<Assembly>();
            _missingDefaultAssemblies = new List<string>();

            if (useDefault)
            {
                foreach (var name in DefaultAssemblyFullNames)
                {
                    try
                    {
                        _assemblies.Add(Assembly.Load(name));
                    }
                    catch (Exception ex)
                    {
                        // not fatal, but degrades 
                        _missingDefaultAssemblies.Add(name);
                        Debug.WriteLine("LENS: default assembly '{0}' could not be loaded: {1}", name, ex.Message);
                    }
                }

                foreach (var anchor in DefaultAssemblyAnchors)
                    AutoReferenceAssembly(anchor.Assembly);

                foreach (var asm in GetLoadedAssemblies())
                    AutoReferenceAssembly(asm);
            }
        }

        #endregion

        #region Fields

        /// <summary>
        /// Full names of assemblies referenced by the script by default.
        /// On .NET Core these exist as type-forwarding facades; the names are also the keys
        /// <see cref="TypeResolver"/> uses to widen the namespace list, so they must stay four-part.
        /// </summary>
        private static readonly string[] DefaultAssemblyFullNames =
        {
            "mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            "System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089",
            "System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"
        };

        /// <summary>
        /// Types whose declaring assemblies back the namespaces imported by default.
        /// </summary>
        private static readonly Type[] DefaultAssemblyAnchors =
        {
            typeof(object), // System
            typeof(Enumerable), // System.Linq
            typeof(Queryable), // System.Linq, again: Core splits Queryable off into its own assembly
            typeof(Expression), // System.Linq.Expressions
            typeof(Regex) // System.Text.RegularExpressions
        };

        /// <summary>
        /// The unique list of referenced assemblies.
        /// </summary>
        private readonly HashSet<Assembly> _assemblies;

        /// <summary>
        /// Default assemblies that could not be loaded. Empty on a healthy runtime.
        /// </summary>
        private readonly List<string> _missingDefaultAssemblies;

        /// <summary>
        /// List of assemblies that can be used by type or extension method resolvers.
        /// </summary>
        public IEnumerable<Assembly> Assemblies => _assemblies.ToList();

        /// <summary>
        /// Names of the default assemblies that failed to load, for diagnostics.
        /// </summary>
        public IEnumerable<string> MissingDefaultAssemblies => _missingDefaultAssemblies;

        /// <summary>
        /// Every namespace the referenced assemblies contain, intermediate ones included.
        ///
        /// Reflecting over every exported type of every assembly is not something compilation would
        /// ever want, so the set is built on first use and only completion ever asks for it.
        /// </summary>
        public IReadOnlyCollection<string> Namespaces => _namespaces ?? (_namespaces = CollectNamespaces());

        /// <summary>
        /// The memoized namespace list.
        /// </summary>
        private HashSet<string> _namespaces;

        #endregion

        #region Methods

        /// <summary>
        /// Register a new assembly as referenced.
        /// </summary>
        public void ReferenceAssembly(Assembly asm)
        {
            _assemblies.Add(asm);
        }

        /// <summary>
        /// Registers an assembly picked up automatically, skipping the runtime's implementation assemblies.
        /// </summary>
        private void AutoReferenceAssembly(Assembly asm)
        {
            // skip BCL facade assemblies
            if (asm.FullName?.StartsWith("System.Private.", StringComparison.Ordinal) == true)
                return;

            _assemblies.Add(asm);
        }

        /// <summary>
        /// Returns the loaded assemblies.
        /// </summary>
        private static IEnumerable<Assembly> GetLoadedAssemblies()
        {
            return AppDomain.CurrentDomain.GetAssemblies();
        }

        /// <summary>
        /// Reads the namespaces off the exported types of every referenced assembly.
        ///
        /// Each namespace is recorded along with all of its prefixes: nothing is declared directly in
        /// System.Collections, and a list of namespaces that cannot get from System to
        /// System.Collections.Generic is not one anybody can walk down.
        ///
        /// The runtime's own implementation assemblies are read as well, although they are
        /// deliberately not referenced: on .NET Core the assemblies that are - mscorlib, System -
        /// are facades that export nothing and merely forward, so System.IO would be missing from a
        /// list built out of the references alone. A type in it resolves perfectly well through the
        /// facade, which is what makes the namespace worth offering.
        /// </summary>
        private HashSet<string> CollectNamespaces()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);

            foreach (var asm in _assemblies.Union(GetLoadedAssemblies()))
            {
                if (asm.IsDynamic)
                    continue;

                Type[] types;

                try
                {
                    types = asm.GetExportedTypes();
                }
                catch (Exception ex)
                {
                    // an assembly whose dependencies are absent cannot be reflected over, and a
                    // completion list missing one namespace beats one that throws
                    Debug.WriteLine(ex);
                    continue;
                }

                foreach (var type in types)
                {
                    var nsp = type.Namespace;
                    if (string.IsNullOrEmpty(nsp) || !result.Add(nsp))
                        continue;

                    for (var dot = nsp.LastIndexOf('.'); dot > 0; dot = nsp.LastIndexOf('.', dot - 1))
                        if (!result.Add(nsp.Substring(0, dot)))
                            break;
                }
            }

            return result;
        }

        #endregion
    }
}
