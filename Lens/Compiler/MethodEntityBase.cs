using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Lens.SyntaxTree.ControlFlow;
using Lens.Utils;

namespace Lens.Compiler
{
	/// <summary>
	/// The base entity for a method and a constructor that allows lookup by argument types.
	/// </summary>
	abstract class MethodEntityBase : TypeContentsBase
	{
		protected MethodEntityBase(bool isImported = false)
		{
			Body = new CodeBlockNode();
			Arguments = new HashList<FunctionArgument>();
			Scope = new Scope();

			IsImported = isImported;
		}

		public bool IsImported;
		public bool IsStatic;

		/// <summary>
		/// The complete argument list with variable names and detailed info.
		/// </summary>
		public HashList<FunctionArgument> Arguments;

		/// <summary>
		/// The types of arguments (for auto-generated methods).
		/// </summary>
		public Type[] ArgumentTypes;

		public CodeBlockNode Body;
		public Scope Scope { get; private set; }

		/// <summary>
		/// The MSIL Generator stream to which commands are emitted.
		/// </summary>
		public ILGenerator Generator { get; protected set; }

		public TryNode CurrentTryBlock { get; set; }
		public CatchNode CurrentCatchBlock { get; set; }

		/// <summary>
		/// Process closures.
		/// </summary>
		public void ProcessClosures()
		{
			var ctx = ContainerType.Context;

			var oldMethod = ctx.CurrentMethod;

			ctx.CurrentMethod = this;
			CurrentTryBlock = null;
			CurrentCatchBlock = null;

			Scope.InitializeScope(ctx);
			Body.ProcessClosures(ctx);
			Scope.FinalizeScope(ctx);

			ctx.CurrentMethod = oldMethod;

		}

		/// <summary>
		/// Compiles the curent method.
		/// </summary>
		public void Compile()
		{
			var ctx = ContainerType.Context;

			var backup = ctx.CurrentMethod;
			ctx.CurrentMethod = this;
			CurrentTryBlock = null;
			CurrentCatchBlock = null;

			emitPrelude(ctx);
			compileCore(ctx);
			emitTrailer(ctx);

			Generator.EmitReturn();

			ctx.CurrentMethod = backup;
		}

		/// <summary>
		/// Gets the information about argument types.
		/// </summary>
		public Type[] GetArgumentTypes(Context ctx)
		{
			return ArgumentTypes ?? Arguments.Values.Select(a => a.GetArgumentType(ctx)).ToArray();
		}

		/// <summary>
		/// Creates closure instances.
		/// </summary>
		protected virtual void emitPrelude(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;
			var closure = Scope.ClosureVariable;
			var closureType = Scope.ClosureType;

			if (closure != null)
			{
				var ctor = closureType.ResolveConstructor(Type.EmptyTypes);

				gen.EmitCreateObject(ctor.ConstructorBuilder);
				gen.EmitSaveLocal(closure);

				try
				{
					var root = closureType.ResolveField(EntityNames.ParentScopeFieldName);
					gen.EmitLoadLocal(closure);
					gen.EmitLoadArgument(0);
					gen.EmitSaveField(root.FieldBuilder);
				}
				catch (KeyNotFoundException) { }
			}

			if (Arguments != null)
			{
				for (var idx = 0; idx < Arguments.Count; idx++)
				{
					var skip = IsStatic ? 0 : 1;
					var arg = Arguments[idx];
					if (arg.IsRefArgument)
						continue;

					var local = Scope.FindName(arg.Name);
					if (local.IsClosured)
					{
						var fi = closureType.ResolveField(local.ClosureFieldName);
						gen.EmitLoadLocal(closure);
						gen.EmitLoadArgument(idx + skip);
						gen.EmitSaveField(fi.FieldBuilder);
					}
					else
					{
						gen.EmitLoadArgument(idx + skip);
						gen.EmitSaveLocal(local);
					}
				}
			}
		}

		protected abstract void compileCore(Context ctx);

		protected virtual void emitTrailer(Context ctx)
		{ }
	}
}
