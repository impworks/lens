using System;
using System.Collections.Generic;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// The scope information of a specific method.
	/// </summary>
	internal class Scope
	{
		public Scope()
		{
			Names = new Dictionary<string, LocalName>();
		}

		/// <summary>
		/// A scope that contains current scope;
		/// </summary>
		public Scope OuterScope;

		/// <summary>
		/// The lookup table of names defined in current scope.
		/// </summary>
		public Dictionary<string, LocalName> Names;

		/// <summary>
		/// The name of the closure class.
		/// </summary>
		public string ClosureClassName { get; private set; }

		#region Methods

		/// <summary>
		/// Gets information about a local name.
		/// </summary>
		public LocalName FindName(string name)
		{
			LocalName local = null;
			find(name, (loc, idx) => local = loc.GetClosuredCopy(idx));
			return local;
		}

		/// <summary>
		/// Declares a new name in the current scope.
		/// </summary>
		public void DeclareName(string name, Type type, bool isConst)
		{
			if(find(name))
				throw new LensCompilerException(string.Format("A variable named '{0}' is already defined!", name));

			Names[name] = new LocalName(name, type, isConst);
		}

		/// <summary>
		/// Checks if the variable is being referenced in another scope.
		/// </summary>
		public void ReferenceName(string name)
		{
			find(
				name,
				(loc, idx) =>
				{
					if (idx > 0 && !loc.IsClosured)
					{
						loc.IsClosured = true;
						// todo: create entity
					}
				}
			);
		}

		/// <summary>
		/// Registers closure entities and assigns IDs to variables.
		/// </summary>
		public void FinalizeScope(Context ctx)
		{
			var idx = 0;
			foreach (var curr in Names.Values)
			{
				if (!curr.IsClosured)
				{
					curr.LocalId = idx;
					idx++;
				}
			}
		}

		/// <summary>
		/// Finds a local name and invoke a callback.
		/// </summary>
		private bool find(string name, Action<LocalName, int> action = null)
		{
			var idx = 0;
			var scope = this;
			while (scope != null)
			{
				LocalName loc;
				if (scope.Names.TryGetValue(name, out loc))
				{
					if(action != null)
						action(loc, idx);
					return true;
				}

				idx++;
				scope = scope.OuterScope;
			}

			return false;
		}

		#endregion
	}
}
