using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Translations;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A conditional expression.
	/// </summary>
	public class IfNode : NodeBase, IStartLocationTrackingEntity
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

		public override LexemLocation EndLocation
		{
			get { return FalseAction == null ? TrueAction.EndLocation : FalseAction.EndLocation; }
			set { LocationSetError(); }
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if (!mustReturn || FalseAction == null)
				return typeof (Unit);

			var type = TrueAction.GetExpressionType(ctx);
			var otherType = FalseAction.GetExpressionType(ctx);
			if (otherType.IsExtendablyAssignableFrom(type))
				return otherType;

			if(!type.IsExtendablyAssignableFrom(otherType))
				Error(CompilerMessages.ConditionInconsistentTyping, type, otherType);

			return type;
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Condition;
			yield return TrueAction;
			yield return FalseAction;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

		    var endLabel = gen.DefineLabel();
			var falseLabel = gen.DefineLabel();
			
			Expr.Cast(Condition, typeof(bool)).Compile(ctx, true);
			if (FalseAction == null)
			{
				gen.EmitBranchFalse(endLabel);
				TrueAction.Compile(ctx, mustReturn);
				if(TrueAction.GetExpressionType(ctx).IsNotVoid())
					gen.EmitPop();

				gen.MarkLabel(endLabel);
				gen.EmitNop();
			}
			else
			{
				var canReturn = mustReturn && FalseAction != null;
                var resultType = GetExpressionType(ctx);

				gen.EmitBranchFalse(falseLabel);
				
                if (TrueAction.GetExpressionType(ctx).IsNotVoid())
                {
                    Expr.Cast(TrueAction, resultType).Compile(ctx, mustReturn);
                    if (!canReturn)
                        gen.EmitPop();
                }
                else
                {
                    TrueAction.Compile(ctx, mustReturn);
                }

				gen.EmitJump(endLabel);

				gen.MarkLabel(falseLabel);
                if (FalseAction.GetExpressionType(ctx).IsNotVoid())
                {
                    Expr.Cast(FalseAction, resultType).Compile(ctx, mustReturn);
                    if (!canReturn)
                        gen.EmitPop();
                }
                else
                {
                    FalseAction.Compile(ctx, mustReturn);
                }

				gen.MarkLabel(endLabel);
				gen.EmitNop();
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
