using System;
using System.Reflection.Emit;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;
using Lens.Utils;

namespace Lens.SyntaxTree.Compiler
{
	/// <summary>
	/// The base entity for a method and a constructor that allows lookup by argument types.
	/// </summary>
	abstract class MethodEntityBase : TypeContentsBase
	{
		protected MethodEntityBase()
		{
			Body = new CodeBlockNode();
			Arguments = new HashList<FunctionArgument>();
			Scope = new Scope();
		}

		/// <summary>
		/// Checks if the method belongs to the type, not its instances.
		/// </summary>
		public bool IsStatic;

		/// <summary>
		/// The argument list.
		/// </summary>
		public HashList<FunctionArgument> Arguments;

		/// <summary>
		/// The types of arguments.
		/// </summary>
		public Type[] ArgumentTypes;

		/// <summary>
		/// The body of the method.
		/// </summary>
		public CodeBlockNode Body;

		/// <summary>
		/// The scope of the method.
		/// </summary>
		public Scope Scope { get; private set; }

		/// <summary>
		/// The MSIL Generator stream to which commands are emitted.
		/// </summary>
		public ILGenerator Generator { get; protected set; }

		/// <summary>
		/// Process closures.
		/// </summary>
		public void ProcessClosures()
		{
			var ctx = ContainerType.Context;

			var oldMethod = ctx.CurrentMethod;
			ctx.CurrentMethod = this;

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

			emitPrelude(ctx);
			compileCore(ctx);
			emitTrailer(ctx);

			Generator.EmitReturn();

			ctx.CurrentMethod = backup;
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

				gen.EmitCreateObject(ctor);
				gen.EmitSaveLocal(closure);
			}

			if (Arguments != null)
			{
				for (var idx = 0; idx < Arguments.Count; idx++)
				{
					var arg = Arguments[idx];
					if (arg.Modifier != ArgumentModifier.In)
						continue;

					var local = Scope.FindName(arg.Name);
					if (local.IsClosured)
					{
						var fi = closureType.ResolveField(local.ClosureFieldName);
						gen.EmitLoadLocal(closure);
						gen.EmitLoadArgument(idx);
						gen.EmitSaveField(fi);
					}
					else
					{
						gen.EmitLoadArgument(idx);
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
