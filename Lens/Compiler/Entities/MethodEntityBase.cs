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
        #region Constructor

        protected MethodEntityBase(TypeEntity type, bool isImported = false) : base(type)
        {
            Arguments = new HashList<FunctionArgument>();

            IsImported = isImported;
        }

        #endregion

        #region Fields

        public bool IsImported;
        public bool IsStatic;

        public CodeBlockNode Body;

        public TryNode CurrentTryBlock { get; set; }
        public CatchNode CurrentCatchBlock { get; set; }

        /// <summary>
        /// The complete argument list with variable names and detailed info.
        /// </summary>
        public HashList<FunctionArgument> Arguments;

        /// <summary>
        /// The types of arguments (for auto-generated methods).
        /// </summary>
        public Type[] ArgumentTypes;

        /// <summary>
        /// The MSIL Generator stream to which commands are emitted.
        /// </summary>
        public ILGenerator Generator { get; protected set; }

        /// <summary>
        /// Checks if the method must return a value.
        /// </summary>
        public abstract bool IsVoid { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Traverses all nodes in the body and expands them if necessary.
        /// </summary>
        public void TransformBody()
        {
            WithContext(ctx =>
                {
                    Body.Scope.RegisterArguments(ctx, IsStatic, Arguments.Values);
                    Body.Transform(ctx, !IsVoid);
                }
            );
        }

        /// <summary>
        /// Process closures.
        /// </summary>
        public void ProcessClosures()
        {
            WithContext(ctx => Body.ProcessClosures(ctx));
        }

        /// <summary>
        /// Emits the body's IL code.
        /// </summary>
        public void Compile()
        {
            WithContext(ctx =>
                {
                    EmitPrelude(ctx);
                    Body.Emit(ctx, !IsVoid);
                    EmitTrailer(ctx);

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

        [DebuggerStepThrough]
        private void WithContext(Action<Context> act)
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

        #region Extension point methods

        /// <summary>
        /// Emits code before the body of the method (if needed).
        /// </summary>
        protected virtual void EmitPrelude(Context ctx)
        {
        }

        /// <summary>
        /// Emits code after the body of the method (if needed).
        /// </summary>
        protected virtual void EmitTrailer(Context ctx)
        {
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return string.Format("{0}.{1}({2})", ContainerType.Name, Name, Arguments.Count);
        }

        #endregion
    }
}