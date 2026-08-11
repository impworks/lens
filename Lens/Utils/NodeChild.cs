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

        public NodeChild(NodeBase node)
        {
            Node = node;
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

        #endregion

        #region Debug

        public override string ToString()
        {
            return Node.ToString();
        }

        #endregion
    }
}