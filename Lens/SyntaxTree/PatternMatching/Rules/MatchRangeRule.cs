using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
    /// <summary>
    /// Checks if an expression is inside a range.
    /// </summary>
    internal class MatchRangeRule : MatchRuleBase
    {
        #region Fields

        /// <summary>
        /// Start of the range.
        /// </summary>
        public MatchLiteralRule RangeStartRule;


        /// <summary>
        /// End of the range.
        /// </summary>
        public MatchLiteralRule RangeEndRule;

        #endregion

        #region Resolve

        public override IEnumerable<PatternNameBinding> Resolve(Context ctx, Type expressionType)
        {
            var startType = RangeStartRule.Literal.LiteralType;
            var endType = RangeEndRule.Literal.LiteralType;

            if (!startType.IsNumericType() || !endType.IsNumericType())
                Error(CompilerMessages.PatternRangeNotNumeric);

            if (!expressionType.IsNumericType())
                Error(CompilerMessages.PatternTypeMismatch, expressionType, "int");

            return NoBindings();
        }

        #endregion

        #region Transform

        public override IEnumerable<NodeBase> Expand(Context ctx, NodeBase expression, Label nextStatement)
        {
            yield return Expr.If(
                Expr.Or(
                    Expr.Less(expression, RangeStartRule.Literal as NodeBase),
                    Expr.Greater(expression, RangeEndRule.Literal as NodeBase)
                ),
                Expr.Block(
                    Expr.JumpTo(nextStatement)
                )
            );
        }

        #endregion

        #region Equality members

        protected bool Equals(MatchRangeRule other)
        {
            return Equals(RangeStartRule, other.RangeStartRule) && Equals(RangeEndRule, other.RangeEndRule);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MatchRangeRule) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((RangeStartRule != null ? RangeStartRule.GetHashCode() : 0) * 397) ^ (RangeEndRule != null ? RangeEndRule.GetHashCode() : 0);
            }
        }

        #endregion
    }
}