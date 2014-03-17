using System;
using Lens.SyntaxTree;

namespace Lens.Utils
{
	internal class NodeChild
	{
		public NodeChild(NodeBase node, Action<NodeBase> setter)
		{
			Node = node;
			Setter = setter;
		}

		public readonly NodeBase Node;
		public readonly Action<NodeBase> Setter;
	}
}
