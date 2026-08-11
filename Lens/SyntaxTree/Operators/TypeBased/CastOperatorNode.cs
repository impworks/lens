using System;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;

namespace Lens.SyntaxTree.Operators.TypeBased
{
    /// <summary>
    /// A node representing a cast expression.
    /// </summary>
    internal class CastOperatorNode : TypeCheckOperatorNodeBase
    {
        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            var type = Type ?? ctx.ResolveType(TypeSignature);
            EnsureLambdaInferred(ctx, Expression, type);
            return type;
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            var fromType = Expression.Resolve(ctx);
            var toType = Resolve(ctx);

            if (TypeEntryCache.Of(fromType).IsNullableType() && !TypeEntryCache.Of(toType).IsNullableType())
                return Expr.Cast(Expr.GetMember(Expression, "Value"), toType);

            return base.Expand(ctx, mustReturn);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            var fromType = Expression.Resolve(ctx);
            var toType = Resolve(ctx);

            if (ctx.Resolver.IsDeclaredTypeParameter(fromType) || ctx.Resolver.IsDeclaredTypeParameter(toType))
                CastGenericParameter(ctx, fromType, toType);

            else if (TypeEntryCache.Of(toType).IsExtendablyAssignableFrom(ctx.Resolver, TypeEntryCache.Of(fromType), true))
                Expression.Emit(ctx, true);

            else if (TypeEntryCache.Of(fromType).IsNumericType() && TypeEntryCache.Of(toType).IsNumericType(true)) // (decimal -> T) is processed via op_Explicit()
                CastNumeric(ctx, fromType, toType);

            else if (fromType.IsCallableType() && toType.IsCallableType())
                CastDelegate(ctx, fromType, toType);

            else if (fromType == typeof(NullType))
            {
                if (TypeEntryCache.Of(toType).IsNullableType())
                {
                    var tmpVar = ctx.Scope.DeclareImplicit(ctx, toType, true);
                    gen.EmitLoadLocal(tmpVar.LocalBuilder, true);
                    gen.EmitInitObject(toType);
                    gen.EmitLoadLocal(tmpVar.LocalBuilder);
                }

                else if (!toType.IsValueType)
                    gen.EmitNull();

                else
                    Error(CompilerMessages.CastNullValueType, toType);
            }

            else if (TypeEntryCache.Of(toType).IsNullableType())
            {
                Expression.Emit(ctx, true);

                var underlying = Nullable.GetUnderlyingType(toType);
                if (TypeEntryCache.Of(underlying).IsNumericType() && TypeEntryCache.Of(fromType).IsNumericType() && underlying != fromType)
                    gen.EmitConvert(underlying);
                else if (underlying != fromType)
                    Error(fromType, toType);

                var ctor = toType.GetConstructor(new[] {underlying});
                gen.EmitCreateObject(ctor);
            }

            else if (TypeEntryCache.Of(toType).IsExtendablyAssignableFrom(ctx.Resolver, TypeEntryCache.Of(fromType)))
            {
                Expression.Emit(ctx, true);

                // box
                if (fromType.IsValueType && toType == typeof(object))
                    gen.EmitBox(fromType);

                else
                {
                    var castOp = ctx.ResolveConvertorToType(fromType, toType);
                    if (castOp != null)
                        gen.EmitCall(castOp.MethodInfo);
                    else
                        gen.EmitCast(toType);
                }
            }

            else if (TypeEntryCache.Of(fromType).IsExtendablyAssignableFrom(ctx.Resolver, TypeEntryCache.Of(toType)))
            {
                Expression.Emit(ctx, true);

                // unbox
                if (fromType == typeof(object) && toType.IsValueType)
                    gen.EmitUnbox(toType);

                // cast ancestor to descendant
                else if (!fromType.IsValueType && !toType.IsValueType)
                    gen.EmitCast(toType);

                else
                {
                    var castOp = ctx.ResolveConvertorToType(fromType, toType);
                    if (castOp != null)
                        gen.EmitCall(castOp.MethodInfo);
                    else
                        Error(fromType, toType);
                }
            }

            else
                Error(fromType, toType);
        }

        /// <summary>
        /// Casts to or from a generic parameter.
        ///
        /// A parameter is neither a reference type nor a value type until it is substituted, so
        /// the usual box / castclass / unbox decision cannot be made from its properties.
        /// 'box' and 'unbox.any' are valid for both kinds and therefore work for every
        /// instantiation of the enclosing declaration.
        /// </summary>
        private void CastGenericParameter(Context ctx, Type from, Type to)
        {
            var gen = ctx.CurrentMethod.Generator;

            Expression.Emit(ctx, true);

            if (from == to)
                return;

            if (from.IsGenericParameter || from.IsValueType)
                gen.EmitBox(from);

            if (to.IsGenericParameter || to.IsValueType)
                gen.EmitUnbox(to);
            else if (to != typeof(object))
                gen.EmitCast(to);
        }

        private void CastDelegate(Context ctx, Type from, Type to)
        {
            var gen = ctx.CurrentMethod.Generator;

            var toCtor = ctx.ResolveConstructor(to, new[] {typeof(object), typeof(IntPtr)});
            var fromMethod = ctx.ResolveMethod(from, "Invoke");
            var toMethod = ctx.ResolveMethod(to, "Invoke");

            var fromArgs = fromMethod.ArgumentTypes;
            var toArgs = toMethod.ArgumentTypes;

            if (fromArgs.Length != toArgs.Length || toArgs.Select((ta, id) => !ta.IsExtendablyAssignableFrom(ctx.Resolver, fromArgs[id], true)).Any(x => x))
                Error(CompilerMessages.CastDelegateArgTypesMismatch, from, to);

            if (!toMethod.ReturnType.IsExtendablyAssignableFrom(ctx.Resolver, fromMethod.ReturnType, true))
                Error(CompilerMessages.CastDelegateReturnTypesMismatch, to, from, toMethod.ReturnType.Materialize(), fromMethod.ReturnType.Materialize());

            if (fromMethod.IsStatic)
                gen.EmitNull();
            else
                Expression.Emit(ctx, true);

            if (from.IsGenericType && to.IsGenericType && from.GetGenericTypeDefinition() == to.GetGenericTypeDefinition())
                return;

            gen.EmitLoadFunctionPointer(fromMethod.MethodInfo);
            gen.EmitCreateObject(toCtor.ConstructorInfo);
        }

        private void CastNumeric(Context ctx, Type from, Type to)
        {
            var gen = ctx.CurrentMethod.Generator;

            Expression.Emit(ctx, true);

            if (to == typeof(decimal))
            {
                var ctor = ctx.ResolveConstructor(typeof(decimal), new[] {from});
                if (ctor == null)
                {
                    ctor = ctx.ResolveConstructor(typeof(decimal), new[] {typeof(int)});
                    gen.EmitConvert(typeof(int));
                }

                gen.EmitCreateObject(ctor.ConstructorInfo);
            }
            else
            {
                gen.EmitConvert(to);
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Displays a default error for uncastable types.
        /// </summary>
        private void Error(Type from, Type to)
        {
            Error(CompilerMessages.CastTypesMismatch, from, to);
        }

        #endregion
    }
}