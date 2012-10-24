using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A set of consecutive code statements.
	/// </summary>
	public class CodeBlockNode : NodeBase
	{
		public CodeBlockNode()
		{
			Statements = new List<NodeBase>();	
		}

		/// <summary>
		/// The statements to execute.
		/// </summary>
		public List<NodeBase> Statements { get; set; }

		public override LexemLocation StartLocation
		{
			get { return Statements.First().StartLocation; }
			set { LocationSetError(); }
		}

		public override LexemLocation EndLocation
		{
			get { return Statements.Last().EndLocation; }
			set { LocationSetError(); }
		}

		public override Type GetExpressionType()
		{
			if (!Statements.Any())
				Error("Code block contains no statements!");

			var last = Statements.Last();
			if (last is VarNode || last is LetNode)
				Error("A {0} declaration cannot be the last statement in a code block.", last is VarNode ? "variable" : "constant");

			return Statements[Statements.Count - 1].GetExpressionType();
		}

		public override void Compile()
		{
			foreach(var curr in Statements)
				curr.Compile();
		}
	}
}
