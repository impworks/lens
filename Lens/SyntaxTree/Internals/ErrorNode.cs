using System;
using Lens.Compiler;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Stands in for a statement the parser could not read.
    ///
    /// Only a tolerant parse produces these. It keeps the tree well-formed - a block always has a
    /// body, an 'if' always has a branch - so that everything around the mistake still binds, which
    /// is the whole point of recovering in the first place.
    ///
    /// It is never emitted: a script containing one has already failed to compile.
    /// </summary>
    internal class ErrorNode : NodeBase
    {
        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            throw new InvalidOperationException("A script that failed to parse cannot be emitted.");
        }

        #endregion

        #region Debug

        public override bool Equals(object obj)
        {
            return obj is ErrorNode;
        }

        public override int GetHashCode()
        {
            return 0;
        }

        public override string ToString()
        {
            return "error()";
        }

        #endregion
    }
}
