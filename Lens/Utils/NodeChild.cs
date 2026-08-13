using System;
using Lens.SyntaxTree;

namespace Lens.Utils
{
    /// <summary>
    /// A single sub-node.
    /// </summary>
    internal class NodeChild
    {
        #region Constructor

        public NodeChild(NodeBase node, bool? mustReturn = null)
        {
            Node = node;
            MustReturn = mustReturn;
        }

        #endregion

        #region Fields

        /// <summary>
        /// The child node itself.
        ///
        /// There is deliberately no setter here. A child used to be replaceable, so that binding
        /// could overwrite a node with its expansion; expansions now live in a side table on the
        /// context and the parse tree is never written to after parsing.
        /// </summary>
        public readonly NodeBase Node;

        /// <summary>
        /// Whether the child has to produce a value, when that does not follow from the statement
        /// around it.
        ///
        /// A condition is a value even inside a method that returns nothing, and the difference is
        /// not cosmetic: an 'if' resolves to Unit when nothing needs its value, and once that has
        /// been recorded it emits nothing for anyone else either.
        /// </summary>
        public readonly bool? MustReturn;

        #endregion

        #region Debug

        public override string ToString()
        {
            return Node.ToString();
        }

        #endregion
    }
}