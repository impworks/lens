using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Translations;

namespace Lens.SyntaxTree.Compiler
{
	public partial class Context
	{
		#region Fields

		/// <summary>
		/// The list of namespaces specified explicitly for safe mode.
		/// </summary>
		private Dictionary<string, bool> _ExplicitNamespaces;

		/// <summary>
		/// The list of namespaces specified explicitly for safe mode.
		/// </summary>
		private Dictionary<string, bool> _ExplicitTypes;

		#endregion

		#region Methods

		private void initSafeMode(LensCompilerOptions opts)
		{
			if (opts.SafeMode == SafeMode.Disabled)
				return;

			Action<string> addNsp = nsp => _ExplicitNamespaces[nsp] = true;
			Action<string> addType = type => _ExplicitTypes[type] = true;

			_ExplicitNamespaces = opts.SafeModeExplicitNamespaces.ToDictionary(n => n, n => true);
			_ExplicitTypes = opts.SafeModeExplicitTypes.ToDictionary(n => n, n => true);

			if (opts.SafeModeExplicitSubsystems.HasFlag(SafeModeSubsystem.Environment))
			{
				addNsp("System.Diagnostics");
				addNsp("System.Runtime");

				addType("System.AppDomain");
				addType("System.AppDomainManager");
				addType("System.Environment");
				addType("System.GC");
			}

			if (opts.SafeModeExplicitSubsystems.HasFlag(SafeModeSubsystem.IO))
			{
				addNsp("System.IO");
			}

			if (opts.SafeModeExplicitSubsystems.HasFlag(SafeModeSubsystem.Threading))
			{
				addNsp("System.Threading");
			}

			if (opts.SafeModeExplicitSubsystems.HasFlag(SafeModeSubsystem.Reflection))
			{
				addNsp("System.Reflection");

				addType("System.AppDomain");
				addType("System.AppDomainManager");
				addType("System.Type");
			}

			if (opts.SafeModeExplicitSubsystems.HasFlag(SafeModeSubsystem.Network))
			{
				addNsp("System.Net");
				addNsp("System.Web");
			}
		}

		public void EnsureAllowed(Type type)
		{
			if (Options.SafeMode == SafeMode.Disabled)
				return;

			var exists = _ExplicitTypes.ContainsKey(type.FullName)
			             || _ExplicitNamespaces.Keys.Any(k => type.Namespace.StartsWith(k));

			if(exists ^ Options.SafeMode == SafeMode.Whitelist)
				Error(CompilerMessages.SafeModeIllegalType, type.FullName);
		}

		#endregion
	}
}
