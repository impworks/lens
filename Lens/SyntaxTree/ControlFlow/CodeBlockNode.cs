using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A set of consecutive code statements.
	/// </summary>
	public class CodeBlockNode : NodeBase, IEnumerable<NodeBase>
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

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if (!Statements.Any())
				Error(CompilerMessages.CodeBlockEmpty);

			var last = Statements.Last();
			if (last is VarNode || last is LetNode)
				Error(last, CompilerMessages.CodeBlockLastVar);

			return Statements[Statements.Count - 1].GetExpressionType(ctx);
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			return Statements;
		}

		protected override void compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			for (var idx = 0; idx < Statements.Count; idx++)
			{
				var subReturn = mustReturn && idx == Statements.Count - 1;
				var curr = Statements[idx];

				var retType = curr.GetExpressionType(ctx, subReturn);

				if (!subReturn && curr.IsConstant)
					continue;

				curr.Compile(ctx, subReturn);

				if(!subReturn && retType.IsNotVoid())
					gen.EmitPop();
			}
		}

		#region Equality members

		protected bool Equals(CodeBlockNode other)
		{
			return Statements.SequenceEqual(other.Statements);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((CodeBlockNode)obj);
		}

		public override int GetHashCode()
		{
			return (Statements != null ? Statements.GetHashCode() : 0);
		}

		#endregion

		#region IEnumerable<NodeBase> implementation

		public IEnumerator<NodeBase> GetEnumerator()
		{
			return Statements.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		public void Add(NodeBase node)
		{
			Statements.Add(node);
		}

		#endregion
	}
}
