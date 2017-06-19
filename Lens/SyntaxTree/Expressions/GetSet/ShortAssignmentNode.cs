using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Lexer;
using Lens.SyntaxTree.Internals;
using Lens.SyntaxTree.Literals;
using Lens.SyntaxTree.Operators.TypeBased;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.GetSet
{
	/// <summary>
	/// Shorthand assignment together with an arithmetic or logic operator.
	/// </summary>
	internal class ShortAssignmentNode : NodeBase
	{
		#region Constructor
		
		public ShortAssignmentNode(LexemType opType, NodeBase expr)
		{
			_operatorType = opType;
			_assignmentOperator = OperatorLookups[opType];
			Expression = expr;
		}

		#endregion

		#region Fields

		/// <summary>
		/// Type of shorthand operator.
		/// </summary>
		private readonly LexemType _operatorType;

		/// <summary>
		/// The kind of operator to use short assignment for.
		/// </summary>
		private readonly Func<NodeBase, NodeBase, NodeBase> _assignmentOperator;

		/// <summary>
		/// Assignment expression to expand.
		/// </summary>
		public NodeBase Expression;

		private readonly static Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>> OperatorLookups = new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
		{
			{ LexemType.And, Expr.And },
			{ LexemType.Or, Expr.Or },
			{ LexemType.Xor, Expr.Xor },
			{ LexemType.ShiftLeft, Expr.ShiftLeft },
			{ LexemType.ShiftRight, Expr.ShiftRight },
			{ LexemType.Plus, Expr.Add },
			{ LexemType.Minus, Expr.Sub },
			{ LexemType.Divide, Expr.Div },
			{ LexemType.Multiply, Expr.Mult },
			{ LexemType.Remainder, Expr.Mod },
			{ LexemType.Power, Expr.Pow },
		};

		#endregion

		#region Transform

		protected override IEnumerable<NodeChild> GetChildren()
		{
			yield return new NodeChild(Expression, x => Expression = x);
		}

		protected override NodeBase Expand(Context ctx, bool mustReturn)
		{
			if (Expression is SetIdentifierNode)
				return ExpandIdentifier(Expression as SetIdentifierNode);

			if (Expression is SetMemberNode)
			{
				var expr = Expression as SetMemberNode;
				return ExpandEvent(ctx, expr) ?? ExpandMember(ctx, expr);
			}

			if (Expression is SetIndexNode)
				return ExpandIndex(ctx, Expression as SetIndexNode);

			throw new InvalidOperationException("Invalid shorthand assignment expression!");
		}

		#endregion

		#region Expansion rules

		/// <summary>
		/// Expands short assignment to an identifier:
		/// x += 1
		/// </summary>
		private NodeBase ExpandIdentifier(SetIdentifierNode node)
		{
			return Expr.Set(
				node.Identifier,
				_assignmentOperator(
					Expr.Get(node.Identifier),
					node.Value
				)
			);
		}

		/// <summary>
		/// Attempts to expand the expression to an event (un)subscription.
		/// </summary>
		private NodeBase ExpandEvent(Context ctx, SetMemberNode node)
		{
			// incorrect operator
			if (!_operatorType.IsAnyOf(LexemType.Plus, LexemType.Minus))
				return null;

			var type = node.StaticType != null
				? ctx.ResolveType(node.StaticType)
				: node.Expression.Resolve(ctx);

			try
			{
				var evt = ctx.ResolveEvent(type, node.MemberName);
//				node.Value = Expr.CastTransparent(node.Value, evt.EventHandlerType);
				return new EventNode(evt, node, _operatorType == LexemType.Plus);
			}
			catch (KeyNotFoundException)
			{
				return null;
			}
		}

		/// <summary>
		/// Expands short assignment to an expression member:
		/// (expr).x += 1
		/// or type::x += 1
		/// </summary>
		private NodeBase ExpandMember(Context ctx, SetMemberNode node)
		{
			// type::name += value
			if (node.StaticType != null)
			{
				return Expr.SetMember(
					node.StaticType,
					node.MemberName,
					_assignmentOperator(
						Expr.GetMember(
							node.StaticType,
							node.MemberName
						),
						node.Value
					)
				);
			}

			// simple case: no need to cache expression
			if (node.Expression is SetIdentifierNode)
			{
				return Expr.SetMember(
					node.Expression,
					node.MemberName,
					_assignmentOperator(
						Expr.GetMember(
							node.Expression,
							node.MemberName
						),
						node.Value
					)
				);
			}

			// (x + y).name += value
			// must cache (x + y) to a local variable to prevent double execution
			var tmpVar = ctx.Scope.DeclareImplicit(ctx, node.Expression.Resolve(ctx), false);
			return Expr.Block(
				Expr.Set(tmpVar, node.Expression),
				Expr.SetMember(
					Expr.Get(tmpVar),
					node.MemberName,
					_assignmentOperator(
						Expr.GetMember(
							Expr.Get(tmpVar),
							node.MemberName
						),
						node.Value
					)
				)
			);
		}

		/// <summary>
		/// Expands short assignment to an array index:
		/// a[x] += 1
		/// </summary>
		private NodeBase ExpandIndex(Context ctx, SetIndexNode node)
		{
			var body = Expr.Block();

			// must cache expression?
			if (!(node.Expression is GetIdentifierNode))
			{
				var tmpExpr = ctx.Scope.DeclareImplicit(ctx, node.Expression.Resolve(ctx), false);
				body.Add(Expr.Set(tmpExpr, node.Expression));
				node.Expression = Expr.Get(tmpExpr);
			}

			// must cache index?
			if (!(node.Index is GetIdentifierNode || node.Index is ILiteralNode || node.Index.IsConstant))
			{
				var tmpIdx = ctx.Scope.DeclareImplicit(ctx, node.Index.Resolve(ctx), false);
				body.Add(Expr.Set(tmpIdx, node.Index));
				node.Index = Expr.Get(tmpIdx);
			}

			body.Add(
				Expr.SetIdx(
					node.Expression,
					node.Index,
					_assignmentOperator(
						Expr.GetIdx(
							node.Expression,
							node.Index
						),
						node.Value
					)
				)
			);

			return body;
		}

		#endregion
	}
}
