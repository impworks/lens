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

		/// <summary>
		/// The ID for the type closured in current scope.
		/// </summary>
		public int ClosureTypeId { get; private set; }

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
		public LocalName DeclareName(string name, Type type, bool isConst)
		{
			if(find(name))
				throw new LensCompilerException(string.Format("A variable named '{0}' is already defined!", name));

			var n = new LocalName(name, type, isConst);
			Names[name] = n;
			return n;
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
			ClosureTypeId = ctx.ClosureId;
			ClosureType = ctx.CreateType(closureName, null, true);

			ctx.ClosureId++;

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

			// create a field for base scope in the current type
			if(OuterScope != null)
				ctx.CreateField(ClosureType.TypeBuilder, "<root>", OuterScope.ClosureType.TypeBuilder);

			// register a variable for closure instance in the scope
			if (ClosureType != null)
			{
				var n = DeclareName(string.Format("<inst_{0}>", ClosureTypeId), ClosureType.TypeBuilder, false);
				n.LocalId = idx;
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
