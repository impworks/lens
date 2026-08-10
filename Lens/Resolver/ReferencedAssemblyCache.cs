using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

        #endregion
    }
}
