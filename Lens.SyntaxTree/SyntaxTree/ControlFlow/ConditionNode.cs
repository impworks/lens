using System;
using System.Collections.Generic;
using System.Reflection;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Translations;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A conditional expression.
	/// </summary>
	public class ConditionNode : NodeBase, IStartLocationTrackingEntity
	{
		public ConditionNode()
		{
			TrueAction = new CodeBlockNode();
		}

		/// <summary>
		/// The condition.
		/// </summary>
		public NodeBase Condition { get; set; }

		/// <summary>
		/// The block of code to be executed if the condition is true.
		/// </summary>
		public CodeBlockNode TrueAction { get; set; }

		/// <summary>
		/// The block of code to be executed if the condition is false.
		/// </summary>
		public CodeBlockNode FalseAction { get; set; }

		public override LexemLocation EndLocation
		{
			get { return FalseAction == null ? TrueAction.EndLocation : FalseAction.EndLocation; }
			set { LocationSetError(); }
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if (!mustReturn)
				return typeof (Unit);

			var type = TrueAction.GetExpressionType(ctx);
			if (FalseAction == null)
				return typeof (Unit);

			var otherType = FalseAction.GetExpressionType(ctx);
			if(type.IsVoid() ^ otherType.IsVoid())
				Error(CompilerMessages.ConditionInconsistentTyping, type, otherType);

			return new[] {type, otherType}.GetMostCommonType();
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Condition;
			yield return TrueAction;
			if (FalseAction != null)
				yield return FalseAction;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var endLabel = gen.DefineLabel();
			var falseLabel = gen.DefineLabel();

			Expr.ErrorScope(Expr.Cast(Condition, typeof(bool)), Condition).Compile(ctx, true);
			if (FalseAction == null)
			{
				gen.EmitBranchFalse(endLabel);
				TrueAction.Compile(ctx, mustReturn);
				gen.MarkLabel(endLabel);
				if(!mustReturn && TrueAction.GetExpressionType(ctx).IsNotVoid())
					gen.EmitPop();
				else
					gen.EmitNop();
			}
			else
			{
				gen.EmitBranchFalse(falseLabel);
				TrueAction.Compile(ctx, mustReturn);

				if (!mustReturn && TrueAction.GetExpressionType(ctx).IsNotVoid())
					gen.EmitPop();

				gen.EmitJump(endLabel);

				gen.MarkLabel(falseLabel);
				FalseAction.Compile(ctx, mustReturn);
				gen.MarkLabel(endLabel);
				if (!mustReturn && FalseAction.GetExpressionType(ctx).IsNotVoid())
					gen.EmitPop();
				else
					gen.EmitNop();
			}
		}

		#region Equality members

		protected bool Equals(ConditionNode other)
		{
			return Equals(Condition, other.Condition) && Equals(TrueAction, other.TrueAction) && Equals(FalseAction, other.FalseAction);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((ConditionNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = (Condition != null ? Condition.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (TrueAction != null ? TrueAction.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (FalseAction != null ? FalseAction.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion
	}
}
