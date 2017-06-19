using System;
using System.Collections.Generic;
using System.Linq;

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
		public bool IsTypeAllowed(Type type)
		{
			if (Options.SafeMode == SafeMode.Disabled)
				return true;

			var genericChecks = !type.IsGenericType || type.GetGenericArguments().All(IsTypeAllowed);
			if (!genericChecks)
				return false;

			var exists = _explicitTypes.ContainsKey(type.FullName) || (type.Namespace != null && _explicitNamespaces.Keys.Any(k => type.Namespace.StartsWith(k)));
			return exists ^ Options.SafeMode == SafeMode.Blacklist;
		}

		#endregion
	}
}
