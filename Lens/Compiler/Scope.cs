using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Lens.Compiler.Entities;
using Lens.Translations;

namespace Lens.Compiler
{
	internal class Scope
	{
		public Scope(ScopeKind kind)
		{
			Locals = new Dictionary<string, Local>();
			Kind = kind;
		}

		#region Fields
		
		/// <summary>
		/// The list of names in current scope.
		/// </summary>
		public readonly Dictionary<string, Local> Locals;

		/// <summary>
		/// The scope that contains the current scope.
		/// </summary>
		public Scope OuterScope;

		/// <summary>
		/// Checks if the the scope is root for a particular method.
		/// </summary>
		public readonly ScopeKind Kind;

		/// <summary>
		/// The type entity that represents current closure.
		/// </summary>
		public TypeEntity ClosureType { get; private set; }

		/// <summary>
		/// The local variable in which the closure is saved.
		/// </summary>
		public LocalBuilder ClosureVariable { get; private set; }

		/// <summary>
		/// Checks if the current closure type must contain a reference to parent closure to reach some of the variables.
		/// </summary>
		public bool ClosureReferencesOuter { get; private set; }

		#endregion

		#region Methods

		/// <summary>
		/// Adds a new local name to current scope.
		/// </summary>
		public Local DeclareLocal(string name, Type type, bool isConst, bool isRefArg = false)
		{
			var local = new Local(name, type, isConst, isRefArg);
			DeclareLocal(local);
			return local;
		}

		/// <summary>
		/// Adds a new local name to current scope.
		/// </summary>
		public void DeclareLocal(Local local)
		{
			if(Locals.ContainsKey(local.Name))
				throw new LensCompilerException(string.Format(CompilerMessages.VariableDefined, local.Name));

			Locals[local.Name] = local;
		}

		/// <summary>
		/// Creates a new implicit local variable or constant.
		/// </summary>
		public Local DeclareImplicit(Context ctx, Type type, bool isConst)
		{
			return DeclareLocal(ctx.Unique.TempVariableName, type, isConst);
		}

		/// <summary>
		/// Finds a local name in current or any parent scopes.
		/// </summary>
		public Local FindLocal(string name)
		{
			var scope = this;
			var dist = 0;
			while (scope != null)
			{
				// try get the variable itself
				Local local;
				if (scope.Locals.TryGetValue(name, out local))
					return local.GetClosuredCopy(dist);

				dist++;
				scope = scope.OuterScope;
			}

			throw new LensCompilerException(string.Format(CompilerMessages.VariableNotFound, name));
		}

		/// <summary>
		/// Registers a name being referenced during closure detection.
		/// </summary>
		public void ReferenceLocal(Context ctx, string name)
		{
			var scope = this;
			var isClosured = false;
			while (scope != null)
			{
				if (scope.Locals.ContainsKey(name))
				{
					if (isClosured)
					{
						var closurable = findScope(s => s.Kind != ScopeKind.Unclosured, scope);
						closurable.ClosureType = ctx.CreateType(ctx.Unique.ClosureName);
						closurable.ClosureType.Kind = TypeEntityKind.Closure;
					}

					return;
				}

				scope.ClosureReferencesOuter = true;
				scope = scope.OuterScope;

				if(scope.Kind != ScopeKind.Unclosured)
					isClosured = true;
			}

			throw new LensCompilerException(string.Format(CompilerMessages.VariableNotFound, name));
		}

		/// <summary>
		/// Creates entities for current scope when it has been left.
		/// </summary>
		public void FinalizeSelf(Context ctx)
		{
			var gen = ctx.CurrentMethod.Generator;
			var closure = findScope(s => s.ClosureType != null).ClosureType;

			// create entities for variables to be excluded
			foreach (var curr in Locals.Values)
			{
				if (curr.IsConstant && curr.IsImmutable && ctx.Options.UnrollConstants)
					continue;

				if (curr.IsClosured)
				{
					curr.ClosureFieldName = ctx.Unique.ClosureFieldName;
					closure.CreateField(curr.ClosureFieldName, curr.Type);
				}
				else
				{
					curr.LocalBuilder = gen.DeclareLocal(curr.Type);
				}
			}

			if (ClosureType != null)
			{
				if (ClosureReferencesOuter)
				{
					// create "Parent" field in the closure type
					var parentType = findScope(s => s.ClosureType != null, OuterScope).ClosureType;
					ClosureType.CreateField(EntityNames.ParentScopeFieldName, parentType.TypeInfo);
				}

				ClosureVariable = ctx.CurrentMethod.Generator.DeclareLocal(ClosureType.TypeInfo);
			}
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Finds closest scope by a condition.
		/// </summary>
		private Scope findScope(Func<Scope, bool> condition, Scope start = null)
		{
			var curr = start ?? this;
			while (curr != null)
			{
				if (condition(curr))
					return curr;

				curr = curr.OuterScope;
			}

			throw new InvalidOperationException("No closure declared! WTF?");
		}

		#endregion
	}

	/// <summary>
	/// Declares the kind of scope, which affects the way its parenthood is instantiated.
	/// </summary>
	internal enum ScopeKind
	{
		/// <summary>
		/// Scope has no 
		/// </summary>
		Unclosured,

		/// <summary>
		/// Scope is the root of a static user-defined function (including Main).
		/// Closure parent is not used.
		/// </summary>
		FunctionRoot,

		/// <summary>
		/// Scope is within a loop.
		/// Closure parent is loaded from a local variable.
		/// </summary>
		Loop,

		/// <summary>
		/// Scope is the root of a lambda function.
		/// Closure parent is loaded from 'this' pointer.
		/// </summary>
		LambdaRoot
	}
}
