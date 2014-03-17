using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.SyntaxTree.Literals;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
	internal class WhileNode : NodeBase
	{
		public WhileNode()
		{
			Body = new CodeBlockNode();	
		}

		public NodeBase Condition { get; set; }

		public CodeBlockNode Body { get; set; }

		protected override Type resolve(Context ctx, bool mustReturn = true)
		{
			return mustReturn ? Body.Resolve(ctx) : typeof(Unit);
		}

		public override IEnumerable<NodeChild> GetChildren()
		{
			yield return new NodeChild(Condition, x => Condition = x);
			foreach(var curr in Body.GetChildren())
				yield return curr;
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			var loopType = Resolve(ctx);
			var saveLast = mustReturn && loopType != typeof (Unit) && loopType != typeof (void) && loopType != typeof (NullType);

			var condType = Condition.Resolve(ctx);
			if(!condType.IsExtendablyAssignableFrom(typeof(bool)))
				error(Condition, CompilerMessages.ConditionTypeMismatch, condType);

			if (Condition.IsConstant && condType == typeof(bool) && Condition.ConstantValue == false && ctx.Options.UnrollConstants)
			{
				if(saveLast)
					Expr.Default(loopType).Emit(ctx, true);

				return;
			}

			var beginLabel = gen.DefineLabel();
			var endLabel = gen.DefineLabel();

			LocalName tmpVar = null;
			if (saveLast)
			{
				tmpVar = ctx.CurrentScopeFrame.DeclareImplicitName(ctx, loopType, false);
				Expr.Set(tmpVar, Expr.Default(loopType));
			}

			gen.MarkLabel(beginLabel);

			Expr.Cast(Condition, typeof(bool)).Emit(ctx, true);
			gen.EmitConstant(false);
			gen.EmitBranchEquals(endLabel);

			Body.Emit(ctx, mustReturn);

			if (saveLast)
				gen.EmitSaveLocal(tmpVar);

			gen.EmitJump(beginLabel);

			gen.MarkLabel(endLabel);
			if (saveLast)
				gen.EmitLoadLocal(tmpVar);
			else
				gen.EmitNop();
		}

		#region Equality members

		protected bool Equals(WhileNode other)
		{
			return Equals(Condition, other.Condition) && Equals(Body, other.Body);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((WhileNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Condition != null ? Condition.GetHashCode() : 0) * 397) ^ (Body != null ? Body.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
