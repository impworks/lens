using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.SyntaxTree.Literals;
using Lens.SyntaxTree.PatternMatching.Rules;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching
{
    /// <summary>
    /// The pattern matching base expression.
    /// </summary>
    internal class MatchNode : NodeBase
    {
        #region Constructor

        public MatchNode()
        {
            MatchStatements = new List<MatchStatementNode>();
            _ruleLabels = new List<Tuple<MatchRuleBase, Label>>();
            _expressionLabels = new Dictionary<MatchStatementNode, Label>();
        }

        #endregion

        #region Fields

        /// <summary>
        /// The expression to match against rules.
        /// </summary>
        public NodeBase Expression;

        /// <summary>
        /// Match statements to test the expression against.
        /// </summary>
        public List<MatchStatementNode> MatchStatements;

        /// <summary>
        /// The label after all blocks to jump to when a successful match has been found.
        /// </summary>
        public Label EndLabel { get; private set; }

        /// <summary>
        /// The label for default value block (if there is supposed to be one).
        /// </summary>
        private Label _defaultLabel;

        /// <summary>
        /// A lookup for rule labels.
        /// </summary>
        private readonly List<Tuple<MatchRuleBase, Label>> _ruleLabels;

        /// <summary>
        /// A lookup for expression labels.
        /// </summary>
        private readonly Dictionary<MatchStatementNode, Label> _expressionLabels;

        /// <summary>
        /// Checks if the current node has been resolved as a value-returning context.
        /// </summary>
        private bool MustReturnValue(Context ctx) => !ctx.FindExpressionType(this).IsVoid();

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            ctx.CheckTypedExpression(Expression, allowNull: true);

            var stmtTypes = new List<TypeEntry>(MatchStatements.Count);
            TypeEntry commonType = null;
            foreach (var stmt in MatchStatements)
            {
                stmt.ParentNode = this;
                stmtTypes.Add(stmt.Resolve(ctx, mustReturn));

                foreach (var rule in stmt.MatchRules)
                {
                    var nameRule = rule as MatchNameRule;
                    // detect catch-all expression for a type
                    if (nameRule != null && stmt.Condition == null)
                    {
                        var nameType = nameRule.Type == null ? TypeEntryCache.Of<object>() : ctx.ResolveType(nameRule.Type);
                        if (commonType != null && commonType.IsExtendablyAssignableFrom(ctx.Resolver, nameType))
                            Error(CompilerMessages.PatternUnreachable);

                        commonType = nameType;
                    }
                }
            }

            return stmtTypes.Any(x => x.IsVoid())
                ? TypeEntryCache.Of<UnitType>()
                : stmtTypes.ToArray().GetMostCommonType(ctx.Resolver);
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Expression);
            foreach (var stmt in MatchStatements)
                yield return new NodeChild(stmt);
        }

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            DefineLabels(ctx);

            var exprType = Expression.Resolve(ctx);

            var block = Expr.Block(ScopeKind.MatchRoot);
            NodeBase exprGetter;
            if (Expression is ILiteralNode || Expression is GetIdentifierNode)
            {
                exprGetter = Expression;
            }
            else
            {
                var tmpVar = ctx.Scope.DeclareImplicit(ctx, exprType, false);
                exprGetter = Expr.Get(tmpVar);
                block.Add(Expr.Set(tmpVar, Expression));
            }

            foreach (var stmt in MatchStatements)
                block.Add(stmt.ExpandRules(ctx, exprGetter, _expressionLabels[stmt]));

            if (MustReturnValue(ctx))
            {
                block.Add(
                    Expr.Block(
                        Expr.JumpLabel(_defaultLabel),

                        // the entry rather than the Type behind it: materializing here would build
                        // the assembly for a match that produces a record, and analysis must not
                        Expr.Default(ctx.FindExpressionType(this))
                    )
                );
            }

            block.Add(Expr.JumpLabel(EndLabel));

            return block;
        }

        #endregion

        #region Label handling

        /// <summary>
        /// Defines jump labels for all statements.
        /// </summary>
        private void DefineLabels(Context ctx)
        {
            foreach (var stmt in MatchStatements)
            {
                _expressionLabels.Add(stmt, DefineLabel(ctx));

                foreach (var rule in stmt.MatchRules)
                    _ruleLabels.Add(Tuple.Create(rule, DefineLabel(ctx)));
            }

            EndLabel = DefineLabel(ctx);

            if (MustReturnValue(ctx))
                _defaultLabel = DefineLabel(ctx);
        }

        /// <summary>
        /// Reserves a jump label, or a placeholder when there is nothing to emit into.
        ///
        /// A label is an emission artefact and its value is only ever read while emitting, so an
        /// analysis-only run - which has no ILGenerator, by design - can hold placeholders. The
        /// labels are matched to statements and rules by the dictionary keys around them, never by
        /// their own value, so placeholders comparing equal to each other changes nothing.
        /// </summary>
        private static Label DefineLabel(Context ctx)
        {
            var generator = ctx.CurrentMethod?.Generator;

            return generator == null
                ? default(Label)
                : generator.DefineLabel();
        }

        /// <summary>
        /// Returns the current and the next labels for a match rule.
        /// </summary>
        public MatchRuleLabelSet GetRuleLabels(Context ctx, MatchRuleBase rule)
        {
            var currIndex = _ruleLabels.FindIndex(x => x.Item1 == rule);
            if (currIndex == -1)
                throw new ArgumentException("Rule does not belong to current match block!");

            var next = currIndex < _ruleLabels.Count - 1
                ? _ruleLabels[currIndex + 1].Item2
                : (MustReturnValue(ctx) ? _defaultLabel : EndLabel);


            return new MatchRuleLabelSet
            {
                CurrentRule = _ruleLabels[currIndex].Item2,
                NextRule = next
            };
        }

        #endregion
    }

    /// <summary>
    /// The set of labels required for each rule.
    /// </summary>
    internal class MatchRuleLabelSet
    {
        /// <summary>
        /// Label that is emitted before first check so that other rules can jump to it.
        /// </summary>
        public Label CurrentRule;

        /// <summary>
        /// Label to jump to if any of the current rule's checks fail.
        /// </summary>
        public Label NextRule;
    }
}