using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Resolver;

namespace Lens.Compiler
{
    internal partial class Context
    {
        #region Fields

        /// <summary>
        /// The list of namespaces specified explicitly for safe mode.
        /// </summary>
        private Dictionary<string, bool> _explicitNamespaces;

        /// <summary>
        /// The list of namespaces specified explicitly for safe mode.
        /// </summary>
        private Dictionary<string, bool> _explicitTypes;

        #endregion

        #region Methods

        /// <summary>
        /// Loads safe mode restrictions into the list of allowed namespaces and types.
        /// </summary>
        private void InitSafeMode()
        {
            if (Options.SafeMode == SafeMode.Disabled)
                return;

            Action<string> addNsp = nsp => _explicitNamespaces[nsp] = true;
            Action<string> addType = type => _explicitTypes[type] = true;

            _explicitNamespaces = Options.SafeModeExplicitNamespaces.ToDictionary(n => n, n => true);
            _explicitTypes = Options.SafeModeExplicitTypes.ToDictionary(n => n, n => true);

            if (Options.SafeModeExplicitSubsystems.HasFlag(SafeModeSubsystem.Environment))
            {
                addNsp("System.Diagnostics");
                addNsp("System.Runtime");

                addType("System.AppDomain");
                addType("System.AppDomainManager");
                addType("System.Environment");
                addType("System.GC");
            }

            if (Options.SafeModeExplicitSubsystems.HasFlag(SafeModeSubsystem.IO))
            {
                addNsp("System.IO");
            }

            if (Options.SafeModeExplicitSubsystems.HasFlag(SafeModeSubsystem.Threading))
            {
                addNsp("System.Threading");
            }

            if (Options.SafeModeExplicitSubsystems.HasFlag(SafeModeSubsystem.Reflection))
            {
                addNsp("System.Reflection");
                addNsp("System.Runtime.Loader");

                addType("System.AppDomain");
                addType("System.AppDomainManager");
                addType("System.Type");
            }

            if (Options.SafeModeExplicitSubsystems.HasFlag(SafeModeSubsystem.Network))
            {
                addNsp("System.Net");
                addNsp("System.Web");
            }
        }

        /// <summary>
        /// Checks if the type is allowed according to the safe mode restrictions.
        /// </summary>
        public bool IsTypeAllowed(TypeEntry type)
        {
            if (Options.SafeMode == SafeMode.Disabled)
                return true;

            // A generic parameter stands for a type rather than being one, so there is nothing here
            // for a rule to name: 'T' is whatever it is substituted with, and that substitution is
            // what the generic argument check below asks about. Deciding it on its own would refuse
            // every generic function and record under a whitelist, and it has no full name to look
            // up under either mode.
            if (type.IsGenericParameter)
                return true;

            var genericChecks = !type.IsGenericType || type.GenericArguments.All(IsTypeAllowed);
            if (!genericChecks)
                return false;

            // An array of a generic parameter has no full name either - T[] is as unnamed as T is -
            // and neither has a type nested in one. A missing name means no rule can match it, the
            // same conclusion the overload below reaches for the same reason.
            var exists = (type.FullName != null && _explicitTypes.ContainsKey(type.FullName))
                         || (type.Namespace != null && _explicitNamespaces.Keys.Any(k => type.Namespace.StartsWith(k)));

            return exists ^ Options.SafeMode == SafeMode.Blacklist;
        }

        /// <summary>
        /// Checks if a CLR type is allowed according to the safe mode restrictions.
        ///
        /// The same question as the overload above, asked of a type that has not been modelled yet:
        /// a completion list weighs every exported type of every referenced assembly, and building a
        /// <see cref="TypeEntry"/> for each of them only to throw it away is work nobody needs.
        /// </summary>
        internal bool IsTypeAllowed(Type type)
        {
            if (Options.SafeMode == SafeMode.Disabled)
                return true;

            var exists = (type.FullName != null && _explicitTypes.ContainsKey(type.FullName))
                         || (type.Namespace != null && _explicitNamespaces.Keys.Any(k => type.Namespace.StartsWith(k)));

            return exists ^ Options.SafeMode == SafeMode.Blacklist;
        }

        #endregion
    }
}