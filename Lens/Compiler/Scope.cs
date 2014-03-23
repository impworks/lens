using System;
using System.Collections.Generic;
using Lens.Compiler.Entities;

namespace Lens.Compiler
{
	/// <summary>
	/// The scope information of a specific method.
	/// </summary>
	internal class Scope
	{
		public Scope()
		{
			RootFrame = new ScopeFrame {Scope = this};
			TotalFrames = new HashSet<ScopeFrame> { RootFrame };
		}

		/// <summary>
		/// The name of the closure class.
		/// </summary>
		public TypeEntity ClosureType { get; private set; }

		/// <summary>
		/// The local variable ID that stores a pointer to current closure object.
		/// </summary>
		public LocalName ClosureVariable { get; private set; }

		/// <summary>
		/// The name of the closure type.
		/// </summary>
		public string ClosureTypeName { get; private set; }

		/// <summary>
		/// The base frame of current scope, in which arguments are declared.
		/// </summary>
		public readonly ScopeFrame RootFrame;

		/// <summary>
		/// The total list of known frames bound to current scope.
		/// </summary>
		private readonly HashSet<ScopeFrame> TotalFrames;

		#region Methods

		/// <summary>
		/// Register arguments as local variables.
		/// </summary>
		public void InitializeScope(Context ctx, ScopeFrame outerFrame)
		{
			var method = ctx.CurrentMethod;
			if (method.Arguments == null)
				return;

			RootFrame.OuterFrame = outerFrame;

			for(var idx = 0; idx < method.Arguments.Count; idx++)
			{
				var arg = method.Arguments[idx];
				try
				{
					var name = RootFrame.DeclareName(arg.Name, arg.Type ?? ctx.ResolveType(arg.TypeSignature), false, arg.IsRefArgument);
					name.ArgumentId = method.IsStatic ? idx : idx + 1;
				}
				catch (LensCompilerException ex)
				{
					ex.BindToLocation(arg);
					throw;
				}
			}
		}

		/// <summary>
		/// Creates a closure type for current closure.
		/// </summary>
		public void CreateClosureType(Context ctx)
		{
			ClosureTypeName = ctx.Unique.ClosureName;
			ClosureType = ctx.CreateType(ClosureTypeName, isSealed: true);
			ClosureType.Kind = TypeEntityKind.Closure;
		}

		/// <summary>
		/// Creates a closured method in the current scope's closure type using function argument records.
		/// </summary>
		public MethodEntity CreateClosureMethod(Context ctx, ScopeFrame currFrame, IEnumerable<FunctionArgument> args, TypeSignature returnType = null)
		{
			return createClosureMethodInternal(ctx, currFrame, name => ClosureType.CreateMethod(name, returnType, args));
		}

		/// <summary>
		/// Creates a closured method in the current scope's closure type using argument types.
		/// </summary>
		public MethodEntity CreateClosureMethod(Context ctx, ScopeFrame currFrame, Type[] args, Type returnType = null)
		{
			return createClosureMethodInternal(ctx, currFrame, name => ClosureType.CreateMethod(name, returnType ?? typeof(Unit), args));
		}

		/// <summary>
		/// Creates a closured method in the current scope's closure type.
		/// </summary>
		public MethodEntity createClosureMethodInternal(Context ctx, ScopeFrame currFrame, Func<string, MethodEntity> creator)
		{
			if (ClosureType == null)
				CreateClosureType(ctx);

			var closureName = string.Format(EntityNames.ClosureMethodNameTemplate, ClosureType.ClosureMethodId);
			ClosureType.ClosureMethodId++;

			var method = creator(closureName);
			method.Scope.RootFrame.OuterFrame = currFrame;
			return method;
		}

		/// <summary>
		/// Registers closure entities and assigns IDs to variables.
		/// </summary>
		public void FinalizeScope(Context ctx)
		{
			foreach (var frame in TotalFrames)
			{
				foreach (var curr in frame.Names.Values)
				{
					if (curr.IsConstant && curr.IsImmutable && ctx.Options.UnrollConstants)
						continue;

					if (curr.IsClosured)
					{
						// create a field in the closured class
						var name = string.Format(EntityNames.ClosureFieldNameTemplate, curr.Name);
						curr.ClosureFieldName = name;
						ClosureType.CreateField(name, curr.Type);
					}
					else
					{
						curr.LocalBuilder = ctx.CurrentILGenerator.DeclareLocal(curr.Type);
					}
				}
			}

			// create a field for base scope in the current type
			if (RootFrame.OuterFrame != null && ClosureType != null)
				ClosureType.CreateField(EntityNames.ParentScopeFieldName, RootFrame.OuterFrame.Scope.ClosureType.TypeBuilder);

			// register a variable for closure instance in the scope
			if (ClosureType != null)
				ClosureVariable = RootFrame.DeclareInternalName(string.Format(EntityNames.ClosureInstanceVariableNameTemplate, ClosureTypeName), ctx, ClosureType.TypeBuilder, false);
		}

		/// <summary>
		/// Add a new frame to the list of known frames.
		/// </summary>
		public void RegisterFrame(ScopeFrame frame)
		{
			TotalFrames.Add(frame);
		}

		#endregion
	}
}
