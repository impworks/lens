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

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            var type = Type != null ? TypeEntryCache.Of(Type) : ctx.ResolveType(TypeSignature);
            EnsureLambdaInferred(ctx, Expression, type);
            return type;
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            var fromType = Expression.Resolve(ctx);
            var toType = Resolve(ctx);

            if (fromType.IsNullableType() && !toType.IsNullableType())
                return Expr.Cast(Expr.GetMember(Expression, "Value"), toType.Materialize());

            return base.Expand(ctx, mustReturn);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            var fromType = Expression.Resolve(ctx);
            var toType = Resolve(ctx);

            if (ctx.Resolver.IsDeclaredTypeParameter(fromType.Materialize()) || ctx.Resolver.IsDeclaredTypeParameter(toType.Materialize()))
                CastGenericParameter(ctx, fromType, toType);

            else if (toType.IsExtendablyAssignableFrom(ctx.Resolver, fromType, true))
                Expression.Emit(ctx, true);

            else if (fromType.IsNumericType() && toType.IsNumericType(true)) // (decimal -> T) is processed via op_Explicit()
                CastNumeric(ctx, fromType, toType);

            else if (fromType.IsCallableType() && toType.IsCallableType())
                CastDelegate(ctx, fromType, toType);

            else if (fromType.Is<NullType>())
            {
                if (toType.IsNullableType())
                {
                    var tmpVar = ctx.Scope.DeclareImplicit(ctx, toType.Materialize(), true);
                    gen.EmitLoadLocal(tmpVar.LocalBuilder, true);
                    gen.EmitInitObject(toType.Materialize());
                    gen.EmitLoadLocal(tmpVar.LocalBuilder);
                }

                else if (!toType.IsValueType)
                    gen.EmitNull();

                else
                    Error(CompilerMessages.CastNullValueType, toType);
            }

            else if (toType.IsNullableType())
            {
                Expression.Emit(ctx, true);

                var underlying = toType.GetNullableUnderlyingType();
                if (underlying.IsNumericType() && fromType.IsNumericType() && underlying != fromType)
                    gen.EmitConvert(underlying.Materialize());
                else if (underlying != fromType)
                    Error(fromType, toType);

                var ctor = toType.Materialize().GetConstructor(new[] {underlying.Materialize()});
                gen.EmitCreateObject(ctor);
            }

            else if (toType.IsExtendablyAssignableFrom(ctx.Resolver, fromType))
            {
                Expression.Emit(ctx, true);

                // box
                if (fromType.IsValueType && toType.Is<object>())
                    gen.EmitBox(fromType.Materialize());

                else
                {
                    var castOp = ctx.ResolveConvertorToType(fromType.Materialize(), toType.Materialize());
                    if (castOp != null)
                        gen.EmitCall(castOp.MethodInfo);
                    else
                        gen.EmitCast(toType.Materialize());
                }
            }

            else if (fromType.IsExtendablyAssignableFrom(ctx.Resolver, toType))
            {
                Expression.Emit(ctx, true);

                // unbox
                if (fromType.Is<object>() && toType.IsValueType)
                    gen.EmitUnbox(toType.Materialize());

                // cast ancestor to descendant
                else if (!fromType.IsValueType && !toType.IsValueType)
                    gen.EmitCast(toType.Materialize());

                else
                {
                    var castOp = ctx.ResolveConvertorToType(fromType.Materialize(), toType.Materialize());
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
        private void CastGenericParameter(Context ctx, TypeEntry from, TypeEntry to)
        {
            var gen = ctx.CurrentMethod.Generator;

            Expression.Emit(ctx, true);

            if (from == to)
                return;

            if (from.IsGenericParameter || from.IsValueType)
                gen.EmitBox(from.Materialize());

            if (to.IsGenericParameter || to.IsValueType)
                gen.EmitUnbox(to.Materialize());
            else if (!to.Is<object>())
                gen.EmitCast(to.Materialize());
        }

        private void CastDelegate(Context ctx, TypeEntry from, TypeEntry to)
        {
            var gen = ctx.CurrentMethod.Generator;

            var toCtor = ctx.ResolveConstructor(to.Materialize(), new[] {typeof(object), typeof(IntPtr)});
            var fromMethod = ctx.ResolveMethod(from.Materialize(), "Invoke");
            var toMethod = ctx.ResolveMethod(to.Materialize(), "Invoke");

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

            if (from.IsGenericType && to.IsGenericType && from.GetGenericDefinition() == to.GetGenericDefinition())
                return;

            gen.EmitLoadFunctionPointer(fromMethod.MethodInfo);
            gen.EmitCreateObject(toCtor.ConstructorInfo);
        }

        private void CastNumeric(Context ctx, TypeEntry from, TypeEntry to)
        {
            var gen = ctx.CurrentMethod.Generator;

            Expression.Emit(ctx, true);

            if (to.Is<decimal>())
            {
                var ctor = ctx.ResolveConstructor(typeof(decimal), new[] {from.Materialize()});
                if (ctor == null)
                {
                    ctor = ctx.ResolveConstructor(typeof(decimal), new[] {typeof(int)});
                    gen.EmitConvert(typeof(int));
                }

                gen.EmitCreateObject(ctor.ConstructorInfo);
            }
            else
            {
                gen.EmitConvert(to.Materialize());
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Displays a default error for uncastable types.
        /// </summary>
        private void Error(TypeEntry from, TypeEntry to)
        {
            Error(CompilerMessages.CastTypesMismatch, from, to);
        }

        #endregion
    }
}