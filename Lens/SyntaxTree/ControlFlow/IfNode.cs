using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A conditional expression.
	/// </summary>
	internal class IfNode : NodeBase
	{
		public IfNode()
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

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			if (!mustReturn || FalseAction == null)
				return typeof (Unit);

			var type = TrueAction.Resolve(ctx);
			var otherType = FalseAction.Resolve(ctx);
			return new[] {type, otherType}.GetMostCommonType();
		}

		public override IEnumerable<NodeChild> GetChildren()
		{
			yield return new NodeChild(Condition, x => Condition = x);

			foreach (var curr in TrueAction.GetChildren())
				yield return curr;

			if(FalseAction != null)
				foreach (var curr in FalseAction.GetChildren())
					yield return curr;
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			var condType = Condition.Resolve(ctx);
			if (!condType.IsExtendablyAssignableFrom(typeof(bool)))
				error(Condition, CompilerMessages.ConditionTypeMismatch, condType);

			if (Condition.IsConstant && ctx.Options.UnrollConstants)
			{
				var node = Condition.ConstantValue ? TrueAction : FalseAction;
				if (node != null)
				{
					node.Emit(ctx, mustReturn);
					if (!mustReturn && node.Resolve(ctx).IsNotVoid())
						gen.EmitPop();
				}

				return;
			}

			var endLabel = gen.DefineLabel();
			var falseLabel = gen.DefineLabel();
			
			Expr.Cast(Condition, typeof(bool)).Emit(ctx, true);
			if (FalseAction == null)
			{
				gen.EmitBranchFalse(endLabel);
				TrueAction.Emit(ctx, mustReturn);
				if (TrueAction.Resolve(ctx).IsNotVoid())
					gen.EmitPop();

				gen.MarkLabel(endLabel);
				gen.EmitNop();
			}
			else
			{
				var canReturn = mustReturn && FalseAction != null;
				var resultType = Resolve(ctx);

				gen.EmitBranchFalse(falseLabel);

				if (TrueAction.Resolve(ctx).IsNotVoid())
				{
					Expr.Cast(TrueAction, resultType).Emit(ctx, mustReturn);
					if (!canReturn)
						gen.EmitPop();
				}
				else
				{
					TrueAction.Emit(ctx, mustReturn);
				}

				gen.EmitJump(endLabel);

				gen.MarkLabel(falseLabel);
				if (FalseAction.Resolve(ctx).IsNotVoid())
				{
					Expr.Cast(FalseAction, resultType).Emit(ctx, mustReturn);
					if (!canReturn)
						gen.EmitPop();
				}
				else
				{
					FalseAction.Emit(ctx, mustReturn);
				}

				gen.MarkLabel(endLabel);
				gen.EmitNop();
			}
		}

		#region Equality members

		protected bool Equals(IfNode other)
		{
			return Equals(Condition, other.Condition) && Equals(TrueAction, other.TrueAction) && Equals(FalseAction, other.FalseAction);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((IfNode)obj);
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
