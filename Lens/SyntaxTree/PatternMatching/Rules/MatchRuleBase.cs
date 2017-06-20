using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using Lens.Compiler;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
    /// <summary>
    /// One particular rule to match againts an expression.
    /// </summary>
    internal abstract class MatchRuleBase : LocationEntity
    {
        /// <summary>
        /// Gets the list of variables bindings declared in the pattern.
        /// </summary>
        public abstract IEnumerable<PatternNameBinding> Resolve(Context ctx, Type expressionType);

        /// <summary>
        /// Returns the AST representation of this rule's checks.
        /// </summary>
        public abstract IEnumerable<NodeBase> Expand(Context ctx, NodeBase expression, Label nextStatement);

        #region Helpers

        /// <summary>
        /// Reports an error bound to an arbitrary location entity.
        /// </summary>
        [ContractAnnotation("=> halt")]
        [DebuggerStepThrough]
        protected void Error(LocationEntity entity, string message, params object[] args)
        {
            throw new LensCompilerException(
                string.Format(message, args),
                entity
            );
        }

        /// <summary>
        /// Reports an error bound to current matching rule.
        /// </summary>
        [ContractAnnotation("=> halt")]
        [DebuggerStepThrough]
        protected void Error(string message, params object[] args)
        {
            Error(this, message, args);
        }

        /// <summary>
        /// Returns an empty sequence of name bindings;
        /// </summary>
        protected IEnumerable<PatternNameBinding> NoBindings()
        {
            yield break;
        }

        /// <summary>
        /// Creates a condition to jump to nextStatement label if the expression is true.
        /// </summary>
        protected NodeBase MakeJumpIf(Label nextStatement, NodeBase condition)
        {
            return Expr.If(condition, Expr.Block(Expr.JumpTo(nextStatement)));
        }

        #endregion
    }
}