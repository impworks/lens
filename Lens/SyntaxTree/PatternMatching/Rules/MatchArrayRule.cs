using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
    /// <summary>
    /// Breaks the array into a sequence of element patterns.
    /// </summary>
    internal class MatchArrayRule : MatchRuleBase
    {
        #region Constructor

        public MatchArrayRule()
        {
            ElementRules = new List<MatchRuleBase>();
        }

        #endregion

        #region Fields

        /// <summary>
        /// The patterns of array items.
        /// </summary>
        public List<MatchRuleBase> ElementRules;

        /// <summary>
        /// The sequence's complete type.
        /// </summary>
        private Type _expressionType;

        /// <summary>
        /// The sequence element's type.
        /// </summary>
        private Type _elementType;

        /// <summary>
        /// Checks whether the source expression is indexable.
        /// </summary>
        private bool _isIndexable;

        /// <summary>
        /// The index of the subsequence item, if any.
        /// </summary>
        private int? _subsequenceIndex;

        /// <summary>
        /// Name of the field that returns the length of the array.
        /// </summary>
        private string SizeMemberName => _expressionType.IsArray ? "Length" : "Count";

        #endregion

        #region Resolve

        public override IEnumerable<PatternNameBinding> Resolve(Context ctx, Type expressionType)
        {
            _expressionType = expressionType;

            if (expressionType.IsArray)
                _elementType = expressionType.GetElementType();

            else if (new[] {typeof(IEnumerable<>), typeof(IList<>)}.Any(expressionType.IsAppliedVersionOf))
                _elementType = expressionType.GetGenericArguments()[0];

            else
                Error(CompilerMessages.PatternTypeMismatch, expressionType, "IEnumerable<T>");

            _isIndexable = !expressionType.IsAppliedVersionOf(typeof(IEnumerable<>));

            for (var idx = 0; idx < ElementRules.Count; idx++)
            {
                var subseq = ElementRules[idx] as MatchNameRule;
                if (subseq != null && subseq.IsArraySubsequence)
                {
                    if (_subsequenceIndex != null)
                        Error(CompilerMessages.PatternArraySubsequences);

                    if (!_isIndexable && idx < ElementRules.Count - 1)
                        Error(CompilerMessages.PatternSubsequenceLocation);

                    _subsequenceIndex = idx;
                }

                var itemType = _subsequenceIndex != idx
                    ? _elementType
                    : (_isIndexable ? _elementType.MakeArrayType() : typeof(IEnumerable<>).MakeGenericType(_elementType));

                var bindings = ElementRules[idx].Resolve(ctx, itemType);
                foreach (var binding in bindings)
                    yield return binding;
            }
        }

        #endregion

        #region Transform

        public override IEnumerable<NodeBase> Expand(Context ctx, NodeBase expression, Label nextStatement)
        {
            if (_subsequenceIndex == null)
            {
                if (_isIndexable)
                {
                    // array size must match exactly
                    yield return MakeJumpIf(
                        nextStatement,
                        Expr.NotEqual(
                            Expr.GetMember(expression, SizeMemberName),
                            Expr.Int(ElementRules.Count)
                        )
                    );
                }

                foreach (var rule in ExpandItemChecksIterated(ctx, expression, ElementRules.Count, nextStatement))
                    yield return rule;

                yield break;
            }

            if (_isIndexable)
            {
                // must contain at least N items
                yield return MakeJumpIf(
                    nextStatement,
                    Expr.Less(
                        Expr.GetMember(expression, SizeMemberName),
                        Expr.Int(ElementRules.Count - 1)
                    )
                );

                var subseqIdx = _subsequenceIndex.Value;

                // pre-subsequence
                for (var idx = 0; idx < subseqIdx; idx++)
                {
                    var rules = ElementRules[idx].Expand(
                        ctx,
                        Expr.GetIdx(expression, Expr.Int(idx)),
                        nextStatement
                    );

                    foreach (var rule in rules)
                        yield return rule;
                }

                var subseqRule = (MatchNameRule) ElementRules[subseqIdx];
                if (!subseqRule.IsWildcard)
                {
                    // subsequence:
                    // x = expr
                    //     |> Skip before // optional
                    //     |> Take (expr.Length - before - after)
                    //     |> ToArray ()
                    var subseqVar = ctx.Scope.DeclareImplicit(ctx, _elementType.MakeArrayType(), false);
                    var subseqExpr = subseqIdx == 0
                        ? expression
                        : Expr.Invoke(expression, "Skip", Expr.Int(subseqIdx));

                    yield return Expr.Set(
                        subseqVar,
                        Expr.Invoke(
                            Expr.Invoke(
                                subseqExpr,
                                "Take",
                                Expr.Sub(
                                    Expr.GetMember(expression, SizeMemberName),
                                    Expr.Int(ElementRules.Count - 1)
                                )
                            ),
                            "ToArray"
                        )
                    );

                    foreach (var rule in subseqRule.Expand(ctx, Expr.Get(subseqVar), nextStatement))
                        yield return rule;
                }

                // post-subsequence
                for (var idx = subseqIdx + 1; idx < ElementRules.Count; idx++)
                {
                    var rules = ElementRules[idx].Expand(
                        ctx,
                        Expr.GetIdx(
                            expression,
                            Expr.Sub(
                                Expr.GetMember(expression, SizeMemberName),
                                Expr.Int(ElementRules.Count - idx)
                            )
                        ),
                        nextStatement
                    );

                    foreach (var rule in rules)
                        yield return rule;
                }
            }
            else
            {
                var itemsCount = ElementRules.Count - 1;
                var checks = ExpandItemChecksIterated(ctx, expression, itemsCount, nextStatement);
                foreach (var check in checks)
                    yield return check;

                // tmpVar = seq.Skip N
                var subseqVar = ctx.Scope.DeclareImplicit(ctx, typeof(IEnumerable<>).MakeGenericType(_elementType), false);
                yield return Expr.Set(
                    subseqVar,
                    Expr.Invoke(
                        expression,
                        "Skip",
                        Expr.Int(itemsCount)
                    )
                );

                foreach (var rule in ElementRules[ElementRules.Count - 1].Expand(ctx, Expr.Get(subseqVar), nextStatement))
                    yield return rule;
            }
        }

        /// <summary>
        /// Checks all items in the array with corresponding rules.
        /// </summary>
        private IEnumerable<NodeBase> ExpandItemChecksIterated(Context ctx, NodeBase expression, int count, Label nextStatement)
        {
            if (count == 0)
                yield break;

            var enumerableType = typeof(IEnumerable<>).MakeGenericType(_elementType);
            var enumeratorType = typeof(IEnumerator<>).MakeGenericType(_elementType);

            var enumeratorVar = ctx.Scope.DeclareImplicit(ctx, enumeratorType, false);

            yield return Expr.Set(
                enumeratorVar,
                Expr.Invoke(
                    Expr.Cast(expression, enumerableType),
                    "GetEnumerator"
                )
            );

            for (var idx = 0; idx < count; idx++)
            {
                // if not iter.MoveNext() then jump!
                yield return MakeJumpIf(
                    nextStatement,
                    Expr.Not(
                        Expr.Invoke(
                            Expr.Get(enumeratorVar),
                            "MoveNext"
                        )
                    )
                );

                var rules = ElementRules[idx].Expand(
                    ctx,
                    Expr.GetMember(
                        Expr.Get(enumeratorVar),
                        "Current"
                    ),
                    nextStatement
                );

                foreach (var rule in rules)
                    yield return rule;
            }
        }

        #endregion

        #region Debug

        protected bool Equals(MatchArrayRule other)
        {
            return ElementRules.SequenceEqual(other.ElementRules);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MatchArrayRule)obj);
        }

        public override int GetHashCode()
        {
            return (ElementRules != null ? ElementRules.GetHashCode() : 0);
        }

        #endregion
    }
}