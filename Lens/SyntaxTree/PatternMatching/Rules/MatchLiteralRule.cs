using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.Literals;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
    /// <summary>
    /// Matches the expression against a literal constant.
    /// </summary>
    internal class MatchLiteralRule : MatchRuleBase
    {
        #region Fields

        /// <summary>
        /// The constant to check against.
        /// </summary>
        public ILiteralNode Literal;

        #endregion

        #region Resolve

        public override IEnumerable<PatternNameBinding> Resolve(Context ctx, Type expressionType)
        {
            if (Literal.LiteralType == typeof(NullType))
            {
                if (expressionType.IsValueType && !expressionType.IsAppliedVersionOf(typeof(Nullable<>)))
                    Error(CompilerMessages.PatternTypeMismatch, expressionType, Literal.LiteralType);
            }
            else if (expressionType != Literal.LiteralType)
            {
                Error(CompilerMessages.PatternTypeMismatch, expressionType, Literal.LiteralType);
            }

            return NoBindings();
        }

        #endregion

        #region Transform

        public override IEnumerable<NodeBase> Expand(Context ctx, NodeBase expression, Label nextStatement)
        {
            yield return Expr.If(
                Expr.NotEqual(Literal as NodeBase, expression),
                Expr.Block(
                    Expr.JumpTo(nextStatement)
                )
            );
        }

        #endregion

        #region Equality members

        protected bool Equals(MatchLiteralRule other)
        {
            return Equals(Literal, other.Literal);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MatchLiteralRule) obj);
        }

        public override int GetHashCode()
        {
            return (Literal != null ? Literal.GetHashCode() : 0);
        }

        #endregion
    }
}