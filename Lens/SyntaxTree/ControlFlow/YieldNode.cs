using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using Lens.Compiler;

namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node that yields a new value in an iterator.
	/// </summary>
	internal class YieldNode : NodeBase, IStartLocationTrackingEntity
	{
		#region Properties

		/// <summary>
		/// The expression to be yielded.
		/// </summary>
		public NodeBase Expression { get; set; }

		/// <summary>
		/// Checks whether the expression represents a single value or a stream of values to be merged into iterator stream.
		/// </summary>
		public bool IsSequence { get; set; }

		/// <summary>
		/// The label that marks current yield statement.
		/// Is used by the iterator dispatcher block to allow function reentrability.
		/// </summary>
		public Label Label { get; private set; }

		public override LexemLocation EndLocation
		{
			get { return Expression.EndLocation; }
			set { LocationSetError(); }
		}

		#endregion

		#region Methods

		public override void ProcessClosures(Context ctx)
		{
			base.ProcessClosures(ctx);

			ctx.CurrentMethod.YieldStatements.Add(this);
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			yield return Expression;
		}

		public void RegisterLabel(Context ctx)
		{
			Label = ctx.CurrentILGenerator.DefineLabel();
		}

		public Type GetIteratorType(Context ctx)
		{
			var type = Expression.GetExpressionType(ctx);

			if (!IsSequence)
				return type;

			if (type == typeof(IEnumerable))
				return typeof(object);

			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
				return type.GetGenericArguments()[0];

			Error("Type {0} is not iterable!", type);
			return null;
		}

		protected override void compile(Context ctx, bool mustReturn)
		{
			if (!IsSequence)
			{
				compileYield(ctx, Expression);
				return;
			}

			var varName = ctx.CurrentScope.DeclareImplicitName(ctx, GetIteratorType(ctx), false);
			var code = Expr.For(
				varName,
				Expression,
				Expr.Block(
					Expr.Dynamic(ct => compileYield(ct, Expr.Get(varName)))
				)
			);

			code.Compile(ctx, mustReturn);
		}

		private void compileYield(Context ctx, NodeBase expr)
		{
			var gen = ctx.CurrentILGenerator;

			var code = Expr.Block(
				Expr.SetMember(Expr.This(), "_Current", expr),
				Expr.SetMember(
					Expr.This(),
					"_StateId",
					Expr.Add(
						Expr.GetMember(Expr.This(), "_StateId"),
						Expr.Int(1)
					)
				)
			);

			code.Compile(ctx, false);

			gen.EmitConstant(true);
			gen.EmitReturn();

			gen.MarkLabel(Label);
			gen.EmitNop();
		}

		#endregion

		#region Equality members

		protected bool Equals(YieldNode other)
		{
			return Equals(Expression, other.Expression) && IsSequence.Equals(other.IsSequence);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((YieldNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return ((Expression != null ? Expression.GetHashCode() : 0) * 397) ^ IsSequence.GetHashCode();
			}
		}

		#endregion

		public override string ToString()
		{
			return string.Format("{0}({1})", IsSequence ? "yieldFrom" : "yield", Expression);
		}
	}
}
