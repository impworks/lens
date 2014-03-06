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
	internal class CodeBlockNode : NodeBase, IEnumerable<NodeBase>
	{
		public CodeBlockNode()
		{
			Statements = new List<NodeBase>();	
			ScopeFrame = new ScopeFrame();
		}

		/// <summary>
		/// The scope frame corresponding to current code block.
		/// </summary>
		private ScopeFrame ScopeFrame;

		/// <summary>
		/// The statements to execute.
		/// </summary>
		public List<NodeBase> Statements { get; set; }

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if (!Statements.Any())
				return typeof (Unit);

			var last = Statements.Last();
			if (last is VarNode || last is LetNode)
				Error(last, CompilerMessages.CodeBlockLastVar);

			ctx.CurrentScope.PushFrame(ScopeFrame);
			var result = Statements[Statements.Count - 1].GetExpressionType(ctx);
			ctx.CurrentScope.PopFrame();

			return result;
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			return Statements;
		}

		protected override void compile(Context ctx, bool mustReturn)
		{
			ctx.CurrentScope.PushFrame(ScopeFrame);

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

			ctx.CurrentScope.PopFrame();
		}

		public override void ProcessClosures(Context ctx)
		{
			ctx.CurrentScope.PushFrame(ScopeFrame);
			base.ProcessClosures(ctx);
			ctx.CurrentScope.PopFrame();
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
