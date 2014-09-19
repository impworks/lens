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

		public NodeChild(NodeBase node, Action<NodeBase> setter)
		{
			Node = node;
			Setter = setter;
		}

		#endregion

		#region Fields

		/// <summary>
		/// The child node itself.
		/// </summary>
		public readonly NodeBase Node;

		/// <summary>
		/// A setter that replaces the current node in its parent.
		/// </summary>
		public readonly Action<NodeBase> Setter;

		#endregion

		#region Debug

		public override string ToString()
		{
			return Node.ToString();
		}

		#endregion
	}
}
