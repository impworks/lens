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
		public TypeEntity ClosureType { get; private set; }

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
		public TypeEntity CreateClosureType(Context ctx)
		{
			var closureName = string.Format("<ClosuredClass{0}>", ctx.ClosureId);
			ctx.ClosureId++;
			ClosureType = ctx.CreateType(closureName, null, true);
			return ClosureType;
		}

		/// <summary>
		/// Creates a closured method in the current scope's closure type.
		/// </summary>
		public MethodEntity CreateClosureMethod(Context ctx, Type[] args)
		{
			if (ClosureType == null)
				ClosureType = CreateClosureType(ctx);

			var closureName = string.Format("<ClosuredMethod{0}>", ClosureType.ClosureMethodId);
			ClosureType.ClosureMethodId++;

			var method = ctx.CreateMethod(ClosureType.TypeBuilder, closureName, args);
			method.Scope.OuterScope = this;
			return method;
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
					ctx.CreateField(ClosureType.TypeBuilder, name, curr.Type);
				}
				else
				{
					// assign local id to the current variable
					curr.LocalId = idx;
					idx++;
				}
			}

			if(OuterScope != null)
				ctx.CreateField(ClosureType.TypeBuilder, "<root>", OuterScope.ClosureType.TypeBuilder);
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
