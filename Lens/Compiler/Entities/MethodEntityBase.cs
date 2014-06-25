using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;
using Lens.SyntaxTree.ControlFlow;
using Lens.Utils;

namespace Lens.Compiler.Entities
{
	/// <summary>
	/// The base entity for a method and a constructor that allows lookup by argument types.
	/// </summary>
	abstract internal class MethodEntityBase : TypeContentsBase
	{
		protected MethodEntityBase(TypeEntity type, bool isImported = false) : base(type)
		{
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

		#region Methods

		/// <summary>
		/// Checks if the method must return a value.
		/// </summary>
		public abstract bool IsVoid { get; }

		/// <summary>
		/// Recursively applies expand transformations to all nodes.
		/// </summary>
		public void TransformBody()
		{
			withContext(ctx =>
				{
					Body.Scope.RegisterArguments(ctx, IsStatic, Arguments.Values);
					Body.Transform(ctx, !IsVoid);
				}
			);
		}

		/// <summary>
		/// Recursively processes closures within current method.
		/// </summary>
		public void ProcessClosures()
		{
			withContext(ctx => Body.ProcessClosures(ctx));
		}

		/// <summary>
		/// Emits the code for the curent method.
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

		/// <summary>
		/// Gets the information about argument types.
		/// </summary>
		public Type[] GetArgumentTypes(Context ctx)
		{
			return ArgumentTypes ?? Arguments.Values.Select(a => a.GetArgumentType(ctx)).ToArray();
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Performs an action with current method's context.
		/// </summary>
		[DebuggerStepThrough]
		private void withContext(Action<Context> act)
		{
			var ctx = ContainerType.Context;

			var oldMethod = ctx.CurrentMethod;
			var oldType = ctx.CurrentType;

			ctx.CurrentMethod = this;
			ctx.CurrentType = ContainerType;
			CurrentTryBlock = null;
			CurrentCatchBlock = null;

			act(ctx);

			ctx.CurrentMethod = oldMethod;
			ctx.CurrentType = oldType;
		}

		#endregion

		#region Overriddable members

		/// <summary>
		/// Emits code before the body of the method.
		/// </summary>
		protected virtual void emitPrelude(Context ctx)
		{ }

		/// <summary>
		/// Emits code after the body of the method.
		/// </summary>
		protected virtual void emitTrailer(Context ctx)
		{ }

		#endregion

		public override string ToString()
		{
			return string.Format("{0}.{1}({2})", ContainerType.Name, Name, Arguments.Count);
		}
	}
}
