using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Lens.SyntaxTree.ControlFlow;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Compiler.Entities
{
	/// <summary>
	/// The base entity for a method and a constructor that allows lookup by argument types.
	/// </summary>
	abstract internal class MethodEntityBase : TypeContentsBase
	{
		protected MethodEntityBase(bool isImported = false)
		{
			Body = new CodeBlockNode();
			Arguments = new HashList<FunctionArgument>();

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

		/// <summary>
		/// The MSIL Generator stream to which commands are emitted.
		/// </summary>
		public ILGenerator Generator { get; protected set; }

		public TryNode CurrentTryBlock { get; set; }
		public CatchNode CurrentCatchBlock { get; set; }

		/// <summary>
		/// Checks if the method must return a value.
		/// </summary>
		public abstract bool IsVoid { get; }

		public void TransformBody()
		{
			withContext(ctx =>
				{
					checkArguments(ctx);
					Body.Transform(ctx, !IsVoid);
				}
			);
		}



		/// <summary>
		/// Process closures.
		/// </summary>
		public void ProcessClosures()
		{
			withContext(ctx => Body.ProcessClosures(ctx));
			Body.Scope.FinalizeSelf(ContainerType.Context);
		}

		/// <summary>
		/// Compiles the curent method.
		/// </summary>
		public void Compile()
		{
			withContext(ctx =>
			    {
				    emitPrelude(ctx);
				    Body.Emit(ctx, !IsVoid);
				    emitTrailer(ctx);

				    Generator.EmitReturn();
			    }
			);
		}

		private void withContext(Action<Context> act)
		{
			var ctx = ContainerType.Context;

			var oldMethod = ctx.CurrentMethod;
			var oldType = ctx.CurrentType;

			ctx.CurrentMethod = this;
			ctx.CurrentType = ContainerType;
			CurrentTryBlock = null;
			CurrentCatchBlock = null;

			ctx.EnterScope(Body.Scope);

			act(ctx);

			ctx.ExitScope();
			
			ctx.CurrentMethod = oldMethod;
			ctx.CurrentType = oldType;
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
		{ }

		protected virtual void emitTrailer(Context ctx)
		{ }

		private void checkArguments(Context ctx)
		{
			if (Arguments == null)
				return;

			var idx = IsStatic ? 0 : 1;
			foreach (var arg in Arguments.Values)
			{
				if (arg.Name == "_")
					Context.Error(arg, CompilerMessages.UnderscoreName);

				var local = new Local(arg.Name, arg.GetArgumentType(ctx), true, arg.IsRefArgument) {ArgumentId = idx};
				Body.Scope.DeclareLocal(local);

				idx++;
			}
		}
	}
}
