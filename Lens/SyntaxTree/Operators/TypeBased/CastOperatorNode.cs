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

            if (fromType.IsNullableType() && !toType.IsNullableType())
                return Expr.Cast(Expr.GetMember(Expression, "Value"), toType.Materialize());

            // the cast is validated here rather than while emitting, because an editor binds the
            // tree and never emits: a check that lives in EmitInternal is a check the editor
            // cannot report
            Validate(ctx, fromType, toType);

            return base.Expand(ctx, mustReturn);
        }

        /// <summary>
        /// Reports the casts that have no representation, following the same case distinction the
        /// emission below makes and stopping at the point where the two differ: what to generate.
        /// </summary>
        private void Validate(Context ctx, TypeEntry fromType, TypeEntry toType)
        {
            // a type parameter is boxed and unboxed, which is valid for every substitution
            if (ctx.Resolver.IsDeclaredTypeParameter(fromType) || ctx.Resolver.IsDeclaredTypeParameter(toType))
                return;

            if (toType.IsExtendablyAssignableFrom(ctx.Resolver, fromType, true))
                return;

            if (fromType.IsNumericType() && toType.IsNumericType(true))
                return;

            if (fromType.IsCallableType() && toType.IsCallableType())
            {
                ValidateDelegate(ctx, fromType, toType);
                return;
            }

            if (fromType.Is<NullType>())
            {
                if (!toType.IsNullableType() && toType.IsValueType)
                    Error(CompilerMessages.CastNullValueType, toType);

                return;
            }

            if (toType.IsNullableType())
            {
                var underlying = toType.GetNullableUnderlyingType();
                if (underlying != fromType && !(underlying.IsNumericType() && fromType.IsNumericType()))
                    Error(fromType, toType);

                return;
            }

            // upcast: boxing, a conversion operator or a plain castclass
            if (toType.IsExtendablyAssignableFrom(ctx.Resolver, fromType))
                return;

            if (fromType.IsExtendablyAssignableFrom(ctx.Resolver, toType))
            {
                // unbox, or a downcast between reference types
                if ((fromType.Is<object>() && toType.IsValueType) || (!fromType.IsValueType && !toType.IsValueType))
                    return;

                if (ctx.ResolveConvertorToType(fromType, toType) == null)
                    Error(fromType, toType);

                return;
            }

            Error(fromType, toType);
        }

        /// <summary>
        /// Checks a cast that is not written in the source but synthesized while emitting - an
        /// argument being adapted to the parameter it is passed to, say. Binding builds one of
        /// these on the side and asks it whether it would work, so that a cast the emitter would
        /// reject is reported by an editor, which binds and never emits.
        /// </summary>
        internal void ValidateSynthesized(Context ctx)
        {
            Validate(ctx, Expression.Resolve(ctx), Resolve(ctx));
        }

        /// <summary>
        /// Checks that one delegate type's signature can stand for another's.
        /// </summary>
        private void ValidateDelegate(Context ctx, TypeEntry from, TypeEntry to)
        {
            var fromMethod = ctx.ResolveMethod(from, "Invoke");
            var toMethod = ctx.ResolveMethod(to, "Invoke");

            var fromArgs = fromMethod.ArgumentTypes;
            var toArgs = toMethod.ArgumentTypes;

            if (fromArgs.Length != toArgs.Length || toArgs.Select((ta, id) => !ta.IsExtendablyAssignableFrom(ctx.Resolver, fromArgs[id], true)).Any(x => x))
                Error(CompilerMessages.CastDelegateArgTypesMismatch, from, to);

            if (!toMethod.ReturnType.IsExtendablyAssignableFrom(ctx.Resolver, fromMethod.ReturnType, true))
                Error(CompilerMessages.CastDelegateReturnTypesMismatch, to, from, toMethod.ReturnType.Materialize(), fromMethod.ReturnType.Materialize());
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
                    var tmpVar = ctx.Scope.DeclareImplicit(ctx, toType, true);
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
                    var castOp = ctx.ResolveConvertorToType(fromType, toType);
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

            var toCtor = ctx.ResolveConstructor(to, new[] {TypeEntryCache.Of<object>(), TypeEntryCache.Of<IntPtr>()});
            var fromMethod = ctx.ResolveMethod(from, "Invoke");

            // a cast node that binding synthesized is emitted without ever being expanded, so the
            // signatures are checked here as well as at bind time
            ValidateDelegate(ctx, from, to);

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
                var ctor = ctx.ResolveConstructor(TypeEntryCache.Of<decimal>(), new[] {from});
                if (ctor == null)
                {
                    ctor = ctx.ResolveConstructor(TypeEntryCache.Of<decimal>(), new[] {TypeEntryCache.Of<int>()});
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