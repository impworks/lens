using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.GetSet
{
    /// <summary>
    /// A node representing write access to a member of a type, field or property.
    /// </summary>
    internal class SetMemberNode : MemberNodeBase
    {
        #region Fields

        private bool _isStatic;
        private PropertyWrapper _property;
        private FieldWrapper _field;

        /// <summary>
        /// Value to be assigned.
        /// </summary>
        public NodeBase Value { get; set; }

        #endregion

        #region Resolve

        protected override Type resolve(Context ctx, bool mustReturn)
        {
            ResolveSelf(ctx);

            return typeof(UnitType);
        }

        /// <summary>
        /// Attempts to resolve member reference to a field or a property.
        /// </summary>
        private void ResolveSelf(Context ctx)
        {
            var type = StaticType != null
                ? ctx.ResolveType(StaticType)
                : Expression.Resolve(ctx);

            CheckTypeInSafeMode(ctx, type);

            // check for field
            try
            {
                _field = ctx.ResolveField(type, MemberName);
                _isStatic = _field.IsStatic;
                if (Expression == null && !_isStatic)
                    Error(CompilerMessages.DynamicMemberFromStaticContext, type, MemberName);
            }
            catch (KeyNotFoundException)
            {
                try
                {
                    _property = ctx.ResolveProperty(type, MemberName);
                    if (!_property.CanSet)
                        Error(CompilerMessages.PropertyNoSetter, MemberName, type);

                    _isStatic = _property.IsStatic;
                    if (Expression == null && !_isStatic)
                        Error(CompilerMessages.DynamicMemberFromStaticContext, type, MemberName);
                }
                catch (KeyNotFoundException)
                {
                    Error(CompilerMessages.TypeSettableIdentifierNotFound, type, MemberName);
                }
            }

            var destType = _field != null ? _field.FieldType : _property.PropertyType;
            EnsureLambdaInferred(ctx, Value, destType);

            var valType = Value.Resolve(ctx);
            ctx.CheckTypedExpression(Value, valType, true);

            if (!destType.IsExtendablyAssignableFrom(valType))
                Error(CompilerMessages.ImplicitCastImpossible, valType, destType);
        }

        #endregion

        #region Transform

        protected override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Expression, x => Expression = x);
            yield return new NodeChild(Value, x => Value = x);
        }

        #endregion

        #region Emit

        protected override void EmitCode(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            var destType = _field != null ? _field.FieldType : _property.PropertyType;

            if (!_isStatic)
            {
                var exprType = Expression.Resolve(ctx);
                if (Expression is IPointerProvider && exprType.IsStruct())
                    (Expression as IPointerProvider).PointerRequired = true;

                Expression.Emit(ctx, true);
            }

            Expr.Cast(Value, destType).Emit(ctx, true);

            if (_field != null)
                gen.EmitSaveField(_field.FieldInfo);
            else
                gen.EmitCall(_property.Setter, true);
        }

        #endregion

        #region Debug

        protected bool Equals(SetMemberNode other)
        {
            return base.Equals(other) && Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((SetMemberNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (base.GetHashCode() * 397) ^ (Value != null ? Value.GetHashCode() : 0);
            }
        }

        public override string ToString()
        {
            return string.Format("setmbr({0} of {1} = {2})", MemberName, Expression, Value);
        }

        #endregion
    }
}