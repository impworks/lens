using System;
using System.Linq;
using Lens.Compiler;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.Translations;

namespace Lens.SyntaxTree.Declarations.Locals
{
    /// <summary>
    /// Shortcut node for multiple variable declarations.
    /// </summary>
    internal class MultiVarNode: NodeBase
    {
        #region Fields

        /// <summary>
        /// Names of the variables to declare.
        /// </summary>
        public string[] Names;

        /// <summary>
        /// Type of the signatures.
        /// </summary>
        public TypeSignature Type;

        /// <summary>
        /// Cached local variable references.
        /// </summary>
        private Local[] _locals;

        #endregion

        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            var type = ctx.ResolveType(Type);
            CheckTypeInSafeMode(ctx, type);

            _locals = new Local[Names.Length];

            for (var idx = 0; idx < Names.Length; idx++)
            {
                if (Names[idx] == "_")
                    Error(CompilerMessages.UnderscoreName);

                _locals[idx] = ctx.Scope.DeclareLocal(Names[idx], type, false);
            }

            return base.ResolveInternal(ctx, mustReturn);
        }

        #endregion

        #region Expand

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            var block = new CodeBlockNode();
            foreach (var name in Names)
            {
                block.Add(
                    new SetIdentifierNode
                    {
                        Identifier = name,
                        IsInitialization = true,
                        Value = Expr.Default(Type)
                    }
                );
            }

            return block;
        }

        #endregion

        #region Debug

        protected bool Equals(MultiVarNode other)
        {
            return Names.SequenceEqual(other.Names) && Equals(Type, other.Type);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MultiVarNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Names != null ? Names.GetHashCode() : 0) * 397) ^ (Type != null ? Type.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            var names = string.Join(", ", Names);
            return $"var({names}:{Type})";
        }

        #endregion
    }
}
