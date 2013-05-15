using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Translations;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The try node.
	/// </summary>
	public class TryNode : NodeBase, IStartLocationTrackingEntity
	{
		public TryNode()
		{
			Code = new CodeBlockNode();
			CatchClauses = new List<CatchNode>();
		}

		/// <summary>
		/// The protected code.
		/// </summary>
		public CodeBlockNode Code { get; set; }

		/// <summary>
		/// The list of catch clauses.
		/// </summary>
		public List<CatchNode> CatchClauses { get; set; }

		/// <summary>
		/// Label to jump to when there's no exception.
		/// </summary>
		public Label EndLabel { get; private set; }

		public override LexemLocation EndLocation
		{
			get { return CatchClauses.Last().EndLocation; }
			set { LocationSetError(); }
		}
		
		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Code;
			foreach(var curr in CatchClauses)
				yield return curr;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var backup = ctx.CurrentTryBlock;
			ctx.CurrentTryBlock = this;

			EndLabel = gen.BeginExceptionBlock();

			Code.Compile(ctx, false);
			gen.EmitLeave(EndLabel);

			var catchTypes = new Dictionary<Type, bool>();
			var catchAll = false;
			foreach (var curr in CatchClauses)
			{
				if(catchAll)
					Error(curr, CompilerMessages.CatchClauseUnreachable);

				var currType = curr.ExceptionType != null ? ctx.ResolveType(curr.ExceptionType) : typeof (Exception);

				if(catchTypes.ContainsKey(currType))
					Error(curr, CompilerMessages.CatchTypeDuplicate, currType);

				if (currType == typeof (Exception))
					catchAll = true;

				curr.Compile(ctx, false);
			}

			gen.EndExceptionBlock();

			ctx.CurrentTryBlock = backup;
		}

		#region Equality members

		protected bool Equals(TryNode other)
		{
			return Equals(Code, other.Code) && CatchClauses.SequenceEqual(other.CatchClauses);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((TryNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Code != null ? Code.GetHashCode() : 0) * 397) ^ (CatchClauses != null ? CatchClauses.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
