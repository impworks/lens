using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
    /// <summary>
    /// The safe block of code.
    /// </summary>
    internal class CatchNode : NodeBase
    {
        #region Constructor

        public CatchNode()
        {
            Code = new CodeBlockNode();
        }

        #endregion

        #region Fields

        /// <summary>
        /// The type of the exception this catch block handles.
        /// Null means any exception.
        /// </summary>
        public TypeSignature ExceptionType { get; set; }

        /// <summary>
        /// A variable to assign the exception to.
        /// </summary>
        public string ExceptionVariable { get; set; }

        /// <summary>
        /// The code block.
        /// </summary>
        public CodeBlockNode Code { get; set; }

        #endregion

        #region Binding

        /// <summary>
        /// What binding learned about this clause.
        /// </summary>
        private class Binding
        {
            /// <summary>
            /// The name the caught exception is assigned to, if the clause names one.
            /// </summary>
            public Local ExceptionVariable;
        }

        #endregion

        #region Resolve

        /// <summary>
        /// Declares the name the exception is caught into.
        ///
        /// This used to happen during closure analysis, which runs after the body has been bound -
        /// so the body could not actually use the name it was given.
        /// </summary>
        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            var type = ExceptionType != null ? ctx.ResolveType(ExceptionType) : TypeEntryCache.Of<Exception>();
            if (!type.Is<Exception>() && !type.IsSubclassOf(TypeEntryCache.Of<Exception>()))
                Error(CompilerMessages.CatchTypeNotException, type);

            if (!string.IsNullOrEmpty(ExceptionVariable))
            {
                var binding = ctx.BindingOf<Binding>(this);
                if (binding.ExceptionVariable == null)
                {
                    binding.ExceptionVariable = ctx.Scope.DeclareLocal(ExceptionVariable, type, false);
                    binding.ExceptionVariable.Declaration = this;
                }
            }

            return base.ResolveInternal(ctx, mustReturn);
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Code);
        }

        #endregion

        #region Closures

        public override void AnalyzeClosures(Context ctx)
        {
            Resolve(ctx);

            base.AnalyzeClosures(ctx);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            var backup = ctx.CurrentCatchBlock;
            ctx.CurrentCatchBlock = this;

            var type = ExceptionType != null ? ctx.ResolveType(ExceptionType).Materialize() : typeof(Exception);
            gen.BeginCatchBlock(type);

            var exceptionVariable = ctx.BindingOf<Binding>(this).ExceptionVariable;
            if (exceptionVariable == null)
            {
                gen.EmitPop();
            }
            else if (exceptionVariable.IsClosured)
            {
                // the closure instance has to be pushed before the value, and the exception is
                // already on the stack, so it waits in a slot of its own for a moment
                var slot = gen.DeclareLocal(exceptionVariable.Type.Materialize());
                gen.EmitSaveLocal(slot);

                var closureType = ctx.Scope.EmitClosureInstance(ctx, exceptionVariable);
                gen.EmitLoadLocal(slot);
                gen.EmitSaveField(ctx.ResolveField(closureType, exceptionVariable.ClosureFieldName).FieldInfo);
            }
            else
            {
                gen.EmitSaveLocal(exceptionVariable.LocalBuilder);
            }

            Code.Emit(ctx, false);

            gen.EmitLeave(ctx.CurrentTryBlock.EndLabel);

            ctx.CurrentCatchBlock = backup;
        }

        #endregion

        #region Debug

        protected bool Equals(CatchNode other)
        {
            return Equals(ExceptionType, other.ExceptionType)
                   && string.Equals(ExceptionVariable, other.ExceptionVariable)
                   && Equals(Code, other.Code);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CatchNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (ExceptionType != null ? ExceptionType.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ExceptionVariable != null ? ExceptionVariable.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Code != null ? Code.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion
    }
}