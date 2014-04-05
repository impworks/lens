using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// The try node.
	/// </summary>
	internal class TryNode : NodeBase
	{
		public TryNode()
		{
			Code = new CodeBlockNode();
			CatchClauses = new List<CatchNode>();
		}

		public CodeBlockNode Code { get; set; }

		public List<CatchNode> CatchClauses { get; set; }

		public CodeBlockNode Finally { get; set; }

		/// <summary>
		/// Label to jump to when there's no exception.
		/// </summary>
		public Label EndLabel { get; private set; }

		public override IEnumerable<NodeChild> GetChildren()
		{
			yield return new NodeChild(Code, null);

			foreach(var curr in CatchClauses)
				yield return new NodeChild(curr, null); // sic! catch clause cannot be replaced

			if(Finally != null)
				yield return new NodeChild(Finally, null);
		}

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var prevTypes = new List<Type>();

			foreach(var curr in CatchClauses)
			{
				var currType = curr.ExceptionType != null ? ctx.ResolveType(curr.ExceptionType) : typeof(Exception);

				foreach (var prevType in prevTypes)
				{
					if(currType == prevType)
						error(curr, CompilerMessages.CatchTypeDuplicate, currType);
					else if(currType.IsExtendablyAssignableFrom(prevType))
						error(curr, CompilerMessages.CatchClauseUnreachable, currType, prevType);
				}

				prevTypes.Add(currType);
			}

			return base.resolve(ctx, mustReturn);
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			var backup = ctx.CurrentTryBlock;
			ctx.CurrentTryBlock = this;

			EndLabel = gen.BeginExceptionBlock();

			Code.Emit(ctx, false);
			gen.EmitLeave(EndLabel);

			foreach (var curr in CatchClauses)
				curr.Emit(ctx, false);

			if (Finally != null)
			{
				gen.BeginFinallyBlock();
				Finally.Emit(ctx, false);
			}

			gen.EndExceptionBlock();

			ctx.CurrentTryBlock = backup;
		}

		#region Equality members

		protected bool Equals(TryNode other)
		{
			return Equals(Code, other.Code)
			       && CatchClauses.SequenceEqual(other.CatchClauses)
			       && Equals(Finally, other.Finally);
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
				int hashCode = (Code != null ? Code.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (CatchClauses != null ? CatchClauses.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Finally != null ? Finally.GetHashCode() : 0);
				return hashCode;
			}
		}
		
		#endregion
	}
}
