using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Declares a name whose type is not known yet.
    ///
    /// The lowering pass invents a name whenever a construct that branches has to produce a value:
    /// the branches assign to it and whatever needed the value reads it, because a value left on
    /// the evaluation stack by one branch is not there when another branch arrives at the same
    /// label. The pass runs before anything has a type, though, and cannot resolve the branch
    /// bodies to find one either - they mention names that only come into being while the construct
    /// around them is bound. So the name is declared with no type at all, and binding works it out
    /// from the assignments, in the order it meets them.
    /// </summary>
    internal class DeferredNameNode : NodeBase
    {
        #region Constructor

        public DeferredNameNode(string name)
        {
            Name = name;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The name being declared.
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// The declared name, once it has been declared.
        /// </summary>
        private Local _local;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            if (_local == null)
            {
                _local = ctx.Scope.DeclareLocal(Name, null, false);
                _local.Declaration = this;
            }

            return base.ResolveInternal(ctx, mustReturn);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            // the name is a slot, and the slot is created along with every other one in the scope:
            // declaring it is the whole of what this node does
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return $"deferred({Name})";
        }

        #endregion
    }
}
