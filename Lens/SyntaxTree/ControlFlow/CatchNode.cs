using System;
using System.Collections.Generic;
using Lens.Compiler;
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

        private Local _exceptionVariable;

        #endregion

        #region Transform

        protected override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Code, null);
        }

        #endregion

        #region Process closures

        public override void ProcessClosures(Context ctx)
        {
            base.ProcessClosures(ctx);

            var type = ExceptionType != null ? ctx.ResolveType(ExceptionType) : typeof(Exception);
            if (type != typeof(Exception) && !type.IsSubclassOf(typeof(Exception)))
                Error(CompilerMessages.CatchTypeNotException, type);

            if (!string.IsNullOrEmpty(ExceptionVariable))
                _exceptionVariable = ctx.Scope.DeclareLocal(ExceptionVariable, type, false);
        }

        #endregion

        #region Emit

        protected override void EmitCode(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            var backup = ctx.CurrentCatchBlock;
            ctx.CurrentCatchBlock = this;

            var type = ExceptionType != null ? ctx.ResolveType(ExceptionType) : typeof(Exception);
            gen.BeginCatchBlock(type);

            if (_exceptionVariable == null)
                gen.EmitPop();
            else
                gen.EmitSaveLocal(_exceptionVariable.LocalBuilder);

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