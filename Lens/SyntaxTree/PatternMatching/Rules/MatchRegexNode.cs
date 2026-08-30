using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
    internal class MatchRegexNode : MatchRuleBase
    {
        #region Static constants

        /// <summary>
        /// Available regex modifiers.
        /// </summary>
        private static readonly Dictionary<char, RegexOptions> AvailableModifiers = new Dictionary<char, RegexOptions>
        {
            {'i', RegexOptions.IgnoreCase},
            {'m', RegexOptions.Multiline},
            {'s', RegexOptions.Singleline},
            {'c', RegexOptions.CultureInvariant}
        };

        #endregion

        #region Fields

        /// <summary>
        /// The source regex string.
        /// </summary>
        public string Value;

        /// <summary>
        /// The modifier string.
        /// </summary>
        public string Modifiers;

        private string _regex;
        private RegexOptions _options;
        private List<PatternNameBinding> _namedGroups;

        #endregion

        #region Resolve

        public override IEnumerable<PatternNameBinding> Resolve(Context ctx, TypeEntry expressionType)
        {
            if (!expressionType.Is<string>())
                Error(CompilerMessages.PatternTypeMismatch, expressionType, typeof(string));

            ParseValue(ctx);
            ParseModifiers();

            return _namedGroups;
        }

        /// <summary>
        /// Finds named groups, checks their types and fixes the regex string.
        /// </summary>
        private void ParseValue(Context ctx)
        {
            if (string.IsNullOrEmpty(Value))
                Error(CompilerMessages.RegexSyntaxError);

            _namedGroups = new List<PatternNameBinding>();
            foreach (var group in RegexLiteral.NamedGroups(Value, StartLocation))
            {
                if (group.Name == "_")
                    Error(CompilerMessages.UnderscoreNameUsed);

                // no type specified: assume string
                if (group.TypeName == null)
                {
                    _namedGroups.Add(new PatternNameBinding(group.Name, TypeEntryCache.Of<string>(), group.NameLocation));
                    continue;
                }

                var type = group.TypeName;
                var actualType = WrapError(
                    () => ctx.ResolveType(type),
                    CompilerMessages.RegexConverterTypeNotFound, type
                );

                WrapError(
                    () => ctx.ResolveMethod(actualType, "TryParse", new[] {TypeEntryCache.Of<string>(), actualType}),
                    CompilerMessages.RegexConverterTypeIncompatible, type
                );

                _namedGroups.Add(new PatternNameBinding(group.Name, actualType, group.NameLocation));
            }

            _regex = RegexLiteral.StripTypes(Value);

            WrapError(() => new Regex(_regex), CompilerMessages.RegexSyntaxError);
        }

        /// <summary>
        /// Converts the string of modifiers into a RegexOptions set.
        /// </summary>
        private void ParseModifiers()
        {
            _options = RegexOptions.ExplicitCapture;
            var last = _options;

            foreach (var mod in Modifiers)
            {
                if (!AvailableModifiers.TryGetValue(mod, out RegexOptions curr))
                    Error(CompilerMessages.RegexUnknownModifier, mod);

                last |= curr;
                if (last == _options)
                    Error(CompilerMessages.RegexDuplicateModifier, mod);

                _options = last;
            }
        }

        #endregion

        #region Transform

        public override IEnumerable<NodeBase> Expand(Context ctx, NodeBase expression, Label nextStatement)
        {
            var regexExpr = Expr.New(
                typeof(Regex),
                Expr.Str(_regex),
                Expr.RawEnum<RegexOptions>((long) _options)
            );

            if (_namedGroups.Count == 0)
            {
                yield return MakeJumpIf(
                    nextStatement,
                    Expr.Not(
                        Expr.Invoke(
                            regexExpr,
                            "IsMatch",
                            expression
                        )
                    )
                );
                yield break;
            }

            // var match = new Regex(...).Match(str)
            // if not match.Success then Jump!
            var matchVar = ctx.Scope.DeclareImplicit(ctx, TypeEntryCache.Of<Match>(), false);
            yield return Expr.Set(
                matchVar,
                Expr.Invoke(
                    regexExpr,
                    "Match",
                    expression
                )
            );
            yield return MakeJumpIf(
                nextStatement,
                Expr.Not(
                    Expr.GetMember(
                        Expr.Get(matchVar),
                        "Success"
                    )
                )
            );

            foreach (var curr in _namedGroups)
            {
                // match.Groups['name'].Value
                var valueExpr = Expr.GetMember(
                    Expr.GetIdx(
                        Expr.GetMember(
                            Expr.Get(matchVar),
                            "Groups"
                        ),
                        Expr.Str(curr.Name)
                    ),
                    "Value"
                );

                if (curr.Type.Is<string>())
                {
                    // name = value
                    yield return Expr.Set(
                        curr.Name,
                        valueExpr
                    );
                }
                else
                {
                    yield return MakeJumpIf(
                        nextStatement,
                        Expr.Not(
                            Expr.Invoke(
                                curr.Type.FullName,
                                "TryParse",
                                valueExpr,
                                Expr.Ref(Expr.Get(curr.Name))
                            )
                        )
                    );
                }
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Return a value if no exception arises, or throw a customized exception otherwise.
        /// </summary>
        private T WrapError<T>(Func<T> action, string msg, params object[] args)
        {
            try
            {
                return action();
            }
            catch
            {
                Error(msg, args);
                return default(T);
            }
        }

        #endregion

        #region Equality members

        protected bool Equals(MatchRegexNode other)
        {
            return string.Equals(Value, other.Value) && string.Equals(Modifiers, other.Modifiers);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MatchRegexNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Value != null ? Value.GetHashCode() : 0) * 397) ^ (Modifiers != null ? Modifiers.GetHashCode() : 0);
            }
        }

        #endregion
    }
}