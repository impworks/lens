using System;
using System.Collections.Generic;
using System.Linq;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The try node.
	/// </summary>
	public class TryNode : NodeBase
	{
		public TryNode()
		{
			CatchClauses = new List<CatchNode>();
		}

		/// <summary>
		/// The protected code.
		/// </summary>
		public CodeBlockNode Code { get; set; }

		/// <summary>
		/// The list of catch clauses.
		/// </summary>
		public List<CatchNode> CatchClauses { get; private set; }

		public override Utils.LexemLocation EndLocation
		{
			get { return CatchClauses.Last().EndLocation; }
			set { throw new InvalidOperationException("Try node's end location cannot be set manually!"); }
		}

		public override Type GetExpressionType()
		{
			return typeof (Unit);
		}

		public override void Compile()
		{
			throw new NotImplementedException();
		}
	}
}
