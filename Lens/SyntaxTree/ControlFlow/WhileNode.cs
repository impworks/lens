using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
	internal class WhileNode : NodeBase
	{
		#region Constructor

		public WhileNode()
		{
			Body = new CodeBlockNode(ScopeKind.Loop);	
		}

		#endregion

		#region Fields

		public NodeBase Condition { get; set; }

		public CodeBlockNode Body { get; protected set; }

		#endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			return mustReturn ? Body.Resolve(ctx) : typeof(UnitType);
		}

		#endregion

		#region Transform

		protected override NodeBase expand(Context ctx, bool mustReturn)
		{
			var loopType = Resolve(ctx);
			var saveLast = mustReturn && !loopType.IsVoid();

			var condType = Condition.Resolve(ctx);
			if (!condType.IsExtendablyAssignableFrom(typeof(bool)))
				error(Condition, CompilerMessages.ConditionTypeMismatch, condType);

			if (Condition.IsConstant && condType == typeof (bool) && Condition.ConstantValue == false && ctx.Options.UnrollConstants)
				return saveLast ? (NodeBase)Expr.Default(loopType) : Expr.Unit();

			return base.expand(ctx, mustReturn);
		}

		protected override IEnumerable<NodeChild> getChildren()
		{
			yield return new NodeChild(Condition, x => Condition = x);
			yield return new NodeChild(Body, null);
		}

		#endregion

		#region Emit

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;
			var loopType = Resolve(ctx);
			var saveLast = mustReturn && !loopType.IsVoid();

			var beginLabel = gen.DefineLabel();
			var endLabel = gen.DefineLabel();

			Local tmpVar = null;
			if (saveLast)
			{
				tmpVar = ctx.Scope.DeclareImplicit(ctx, loopType, false);
				Expr.Set(tmpVar, Expr.Default(loopType)).Emit(ctx, false);
			}

			gen.MarkLabel(beginLabel);

			Expr.Cast(Condition, typeof(bool)).Emit(ctx, true);
			gen.EmitConstant(false);
			gen.EmitBranchEquals(endLabel);

			Body.Emit(ctx, mustReturn);

			if (saveLast)
				gen.EmitSaveLocal(tmpVar.LocalBuilder);

			gen.EmitJump(beginLabel);

			gen.MarkLabel(endLabel);
			if (saveLast)
				gen.EmitLoadLocal(tmpVar.LocalBuilder);
		}

		#endregion

		#region Debug

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
