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
			find(name, (loc, idx) => loc.IsClosured |= idx > 0);
		}

		/// <summary>
		/// Creates a closure type for current closure.
		/// </summary>
		public void CreateClosureType(Context ctx)
		{
			ClosureClassName = string.Format("<ClosuredClass{0}>", ctx.ClosureId);
			ctx.ClosureId++;
			ctx.CreateType(ClosureClassName, null, true);
		}

		/// <summary>
		/// Registers closure entities and assigns IDs to variables.
		/// </summary>
		public void FinalizeScope(Context ctx)
		{
			var idx = 0;
			foreach (var curr in Names.Values)
			{
				if (curr.IsClosured)
				{
					// create a field in the closured class
					var name = string.Format("<f_{0}>", curr.Name);
					ctx.CreateField(ClosureClassName, name, curr.Type);
				}
				else
				{
					// assign local id to the current variable
					curr.LocalId = idx;
					idx++;
				}
			}

			if(OuterScope != null)
				ctx.CreateField(ClosureClassName, "<root>", OuterScope.ClosureClassName);
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
