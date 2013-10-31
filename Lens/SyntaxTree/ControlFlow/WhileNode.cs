using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.SyntaxTree.Literals;

namespace Lens.SyntaxTree.ControlFlow
{
	internal class WhileNode : NodeBase, IStartLocationTrackingEntity
	{
		public WhileNode()
		{
			Body = new CodeBlockNode();	
		}

		public NodeBase Condition { get; set; }

		public CodeBlockNode Body { get; set; }

		public override LexemLocation EndLocation
		{
			get { return Body.EndLocation; }
			set { LocationSetError(); }
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return mustReturn ? Body.GetExpressionType(ctx) : typeof(Unit);
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Condition;
			yield return Body;
		}

		protected override void compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			var loopType = GetExpressionType(ctx);
			var saveLast = mustReturn && loopType != typeof (Unit) && loopType != typeof (void) && loopType != typeof (NullType);

			if (Condition.IsConstant && Condition.ConstantValue == false && ctx.Options.UnrollConstants)
			{
				if(saveLast)
					Expr.Default(loopType).Compile(ctx, true);

				return;
			}

			var beginLabel = gen.DefineLabel();
			var endLabel = gen.DefineLabel();

			LocalName tmpVar = null;
			if (saveLast)
				tmpVar = ctx.CurrentScope.DeclareImplicitName(loopType, false);

			gen.MarkLabel(beginLabel);

			Expr.Cast(Condition, typeof(bool)).Compile(ctx, true);
			gen.EmitConstant(false);
			gen.EmitBranchEquals(endLabel);

			if (saveLast)
				SaveToTempLocal(ctx, tmpVar, () => Body.Compile(ctx, mustReturn));
			else
				Body.Compile(ctx, mustReturn);

			gen.EmitJump(beginLabel);

			gen.MarkLabel(endLabel);
			if (saveLast)
				LoadFromTempLocal(ctx, tmpVar);
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
