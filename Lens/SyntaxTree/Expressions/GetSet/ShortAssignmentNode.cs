using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Lexer;
using Lens.Resolver;
using Lens.SyntaxTree.Internals;
using Lens.SyntaxTree.Literals;
using System.Linq;
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

        /// <summary>
        /// Map of lexems to corresponding node constructors (for expansion).
        /// Must have an entry for every lexem the parser accepts before the '=' of a shorthand
        /// assignment - see LensParser.BinaryOperators - since any of them reaches the constructor.
        /// </summary>
        private static readonly Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>> OperatorLookups = new Dictionary<LexemType, Func<NodeBase, NodeBase, NodeBase>>
        {
            {LexemType.BitAnd, Expr.BitAnd},
            {LexemType.BitOr, Expr.BitOr},
            {LexemType.BitXor, Expr.BitXor},
            {LexemType.And, Expr.And},
            {LexemType.Or, Expr.Or},
            {LexemType.Xor, Expr.Xor},
            {LexemType.ShiftLeft, Expr.ShiftLeft},
            {LexemType.ShiftRight, Expr.ShiftRight},
            {LexemType.Plus, Expr.Add},
            {LexemType.Minus, Expr.Sub},
            {LexemType.Divide, Expr.Div},
            {LexemType.Multiply, Expr.Mult},
            {LexemType.Remainder, Expr.Mod},
            {LexemType.Power, Expr.Pow},
        };

        /// <summary>
        /// Map of lexems to the names of the instance compound assignment operators declared for
        /// them. Such an operator mutates its target in place, so an expression that has one is not
        /// read, combined and written back.
        /// '&amp;&amp;=' and '||=' are absent on purpose: they only sometimes evaluate their
        /// right-hand side, and a call would always evaluate it. '**=' has no metadata name.
        /// </summary>
        private static readonly Dictionary<LexemType, string> InPlaceOperatorNames = new Dictionary<LexemType, string>
        {
            {LexemType.BitAnd, "op_BitwiseAndAssignment"},
            {LexemType.BitOr, "op_BitwiseOrAssignment"},
            {LexemType.BitXor, "op_ExclusiveOrAssignment"},
            {LexemType.Xor, "op_ExclusiveOrAssignment"},
            {LexemType.ShiftLeft, "op_LeftShiftAssignment"},
            {LexemType.ShiftRight, "op_RightShiftAssignment"},
            {LexemType.Plus, "op_AdditionAssignment"},
            {LexemType.Minus, "op_SubtractionAssignment"},
            {LexemType.Divide, "op_DivisionAssignment"},
            {LexemType.Multiply, "op_MultiplicationAssignment"},
            {LexemType.Remainder, "op_ModulusAssignment"},
        };

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Expression);
        }

        /// <summary>
        /// '&amp;&amp;=' and '||=' expand into the operator they are named after, and that operator
        /// only sometimes evaluates its right-hand side. The rest expand into one that always does.
        /// </summary>
        internal override IReadOnlyList<NodeBase> Operands =>
            _operatorType == LexemType.And || _operatorType == LexemType.Or
                ? NoOperands
                : new[] {Expression};

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            var copy = Copy<ShortAssignmentNode>();
            copy.Expression = operands[0];
            return copy;
        }

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            if (Expression is SetIdentifierNode)
                return ExpandIdentifier(ctx, Expression as SetIdentifierNode);

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
        private NodeBase ExpandIdentifier(Context ctx, SetIdentifierNode node)
        {
            var inPlace = ExpandInPlace(ctx, Expr.Get(node.Identifier), node.Value);
            if (inPlace != null)
                return inPlace;

            return Expr.Set(
                node.Identifier,
                _assignmentOperator(
                    Expr.Get(node.Identifier),
                    node.Value
                )
            );
        }

        /// <summary>
        /// Attempts to expand the expression into a call to the instance compound assignment
        /// operator of the target's own type, which updates the target in place.
        /// Returns null when the type declares no such operator, leaving the read-modify-write
        /// expansion to say what the shorthand means.
        /// </summary>
        private NodeBase ExpandInPlace(Context ctx, NodeBase target, NodeBase value)
        {
            if (!InPlaceOperatorNames.TryGetValue(_operatorType, out var name))
                return null;

            MethodWrapper method;
            try
            {
                method = ctx.ResolveMethod(target.Resolve(ctx), name, new[] {value.Resolve(ctx)});
            }
            catch
            {
                return null;
            }

            // a static or value-returning member of that name is an ordinary method that happens to
            // be named like an operator: only the instance void one carries the operator's meaning
            if (method == null || method.IsStatic || !method.ReturnType.IsVoid())
                return null;

            return Expr.Invoke(target, name, value);
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
                var inPlaceStatic = ExpandInPlace(ctx, Expr.GetMember(node.StaticType, node.MemberName), node.Value);
                if (inPlaceStatic != null)
                    return inPlaceStatic;

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
                var inPlaceSimple = ExpandInPlace(ctx, Expr.GetMember(node.Expression, node.MemberName), node.Value);
                if (inPlaceSimple != null)
                    return inPlaceSimple;

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
            var inPlaceCached = ExpandInPlace(ctx, Expr.GetMember(Expr.Get(tmpVar), node.MemberName), node.Value);
            if (inPlaceCached != null)
            {
                return Expr.Block(
                    Expr.Set(tmpVar, node.Expression),
                    inPlaceCached
                );
            }

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

            // must cache the indexes? every dimension is read twice, so anything with a side
            // effect - or merely worth computing once - is evaluated into a temporary first
            for (var idx = 0; idx < node.Indexes.Count; idx++)
            {
                var curr = node.Indexes[idx];
                if (curr is GetIdentifierNode || curr is ILiteralNode || curr.IsConstant)
                    continue;

                var tmpIdx = ctx.Scope.DeclareImplicit(ctx, curr.Resolve(ctx), false);
                body.Add(Expr.Set(tmpIdx, curr));
                node.Indexes[idx] = Expr.Get(tmpIdx);
            }

            var getter = new GetIndexNode {Expression = node.Expression, Indexes = node.Indexes.ToList()};

            var inPlace = ExpandInPlace(ctx, getter, node.Value);
            if (inPlace != null)
            {
                body.Add(inPlace);
                return body;
            }

            var setter = new SetIndexNode
            {
                Expression = node.Expression,
                Indexes = node.Indexes.ToList(),
                Value = _assignmentOperator(getter, node.Value)
            };

            body.Add(setter);

            return body;
        }

        #endregion
    }
}