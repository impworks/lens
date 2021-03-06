﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.PatternMatching.Rules
{
    /// <summary>
    /// Checks if the expression is of a specified record type and applies a pattern to its fields.
    /// </summary>
    internal class MatchRecordRule : MatchRuleBase
    {
        #region Constructor

        public MatchRecordRule()
        {
            FieldRules = new List<MatchRecordField>();
        }

        #endregion

        #region Fields

        /// <summary>
        /// Type signature.
        /// </summary>
        public TypeSignature Identifier;

        /// <summary>
        /// Nested rule of the label.
        /// </summary>
        public List<MatchRecordField> FieldRules;

        /// <summary>
        /// The actual type.
        /// </summary>
        private Type _type;

        #endregion

        #region Resolve

        public override IEnumerable<PatternNameBinding> Resolve(Context ctx, Type expressionType)
        {
            var typeEntity = ctx.FindType(Identifier.FullSignature);
            if (typeEntity == null || (!typeEntity.Kind.IsAnyOf(TypeEntityKind.Record)))
                Error(Identifier, CompilerMessages.PatternNotValidRecord, Identifier.FullSignature);

            _type = ctx.ResolveType(Identifier);
            if (!_type.IsExtendablyAssignableFrom(expressionType) && !expressionType.IsExtendablyAssignableFrom(_type))
                Error(CompilerMessages.PatternTypeMatchImpossible, _type, expressionType);

            var duplicate = FieldRules.GroupBy(x => x.Name).FirstOrDefault(x => x.Count() > 1);
            if (duplicate != null)
                Error(CompilerMessages.PatternRecordFieldDuplicated, duplicate.Key);

            var subBindings = new List<PatternNameBinding>();
            foreach (var fieldRule in FieldRules)
            {
                try
                {
                    var field = typeEntity.ResolveField(fieldRule.Name.FullSignature);
                    subBindings.AddRange(fieldRule.Rule.Resolve(ctx, field.Type));
                }
                catch (KeyNotFoundException)
                {
                    Error(fieldRule.Name, CompilerMessages.PatternRecordNoField, Identifier.FullSignature, fieldRule.Name.FullSignature);
                }
            }

            return subBindings;
        }

        #endregion

        #region Transform

        public override IEnumerable<NodeBase> Expand(Context ctx, NodeBase expression, Label nextStatement)
        {
            yield return MakeJumpIf(
                nextStatement,
                Expr.Not(Expr.Is(expression, _type))
            );

            foreach (var fieldRule in FieldRules)
            {
                var rules = fieldRule.Rule.Expand(
                    ctx,
                    Expr.GetMember(Expr.Cast(expression, _type), fieldRule.Name.FullSignature),
                    nextStatement
                );

                foreach (var rule in rules)
                    yield return rule;
            }
        }

        #endregion

        #region Equality members

        protected bool Equals(MatchRecordRule other)
        {
            return Equals(Identifier, other.Identifier) && FieldRules.SequenceEqual(other.FieldRules);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MatchRecordRule) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Identifier != null ? Identifier.GetHashCode() : 0) * 397) ^ (FieldRules != null ? FieldRules.GetHashCode() : 0);
            }
        }

        #endregion
    }

    /// <summary>
    /// One particular record field pattern.
    /// </summary>
    internal class MatchRecordField : LocationEntity
    {
        /// <summary>
        /// Name of the field.
        /// </summary>
        public TypeSignature Name;

        /// <summary>
        /// Rule to apply to field's value.
        /// </summary>
        public MatchRuleBase Rule;

        #region Equality members

        protected bool Equals(MatchRecordField other)
        {
            return Equals(Name, other.Name) && Equals(Rule, other.Rule);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MatchRecordField) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (Rule != null ? Rule.GetHashCode() : 0);
            }
        }

        #endregion
    }
}