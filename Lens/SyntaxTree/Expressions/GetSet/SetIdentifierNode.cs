using System;
using System.Collections.Generic;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.GetSet
{
    /// <summary>
    /// A node representing read access to a local variable or a function.
    /// </summary>
    internal class SetIdentifierNode : IdentifierNodeBase
    {
        #region Constructor

        public SetIdentifierNode(string identifier = null)
        {
            Identifier = identifier;
        }

        #endregion

        #region Fields

        /// <summary>
        /// A flag indicating that assignment to a constant variable is legal
        /// because it's being instantiated.
        /// </summary>
        public bool IsInitialization { get; set; }

        /// <summary>
        /// Value to be assigned.
        /// </summary>
        public NodeBase Value { get; set; }

        /// <summary>
        /// Global property reference (if resolved).
        /// </summary>
        private GlobalPropertyInfo _property;

        #endregion

        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            if (Identifier == "_")
                Error(CompilerMessages.UnderscoreNameUsed);

            var nameInfo = Local ?? ctx.Scope.FindLocal(Identifier);
            if (nameInfo != null)
            {
                // an assignment names the variable just as a read does
                if (Identifier != null)
                    nameInfo.Reference(this);

                if (nameInfo.IsImmutable && !IsInitialization)
                    Error(CompilerMessages.IdentifierIsConstant, Identifier);
            }
            else
            {
                try
                {
                    _property = ctx.ResolveGlobalProperty(Identifier);

                    if (!_property.HasSetter)
                        Error(CompilerMessages.GlobalPropertyNoSetter, Identifier);
                }
                catch (KeyNotFoundException)
                {
                    Error(CompilerMessages.VariableNotFound, Identifier);
                }
            }

            var destType = nameInfo != null ? nameInfo.Type : _property.PropertyType;
            EnsureLambdaInferred(ctx, Value, destType);

            var exprType = Value.Resolve(ctx);
            ctx.CheckTypedExpression(Value, exprType, true);

            if (!TypeEntryCache.Of(destType).IsExtendablyAssignableFrom(ctx.Resolver, TypeEntryCache.Of(exprType)))
            {
                Error(
                    nameInfo != null ? CompilerMessages.IdentifierTypeMismatch : CompilerMessages.GlobalPropertyTypeMismatch,
                    exprType,
                    destType
                );
            }

            return base.ResolveInternal(ctx, mustReturn);
        }

        #endregion

        #region Transform

        protected override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Value);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            var type = Value.Resolve(ctx);

            if (_property != null)
            {
                var cast = Expr.Cast(Value, type);
                if (_property.SetterMethod != null)
                {
                    cast.Emit(ctx, true);
                    gen.EmitCall(_property.SetterMethod.MethodInfo);
                }
                else
                {
                    var method = typeof(GlobalPropertyHelper).GetMethod("Set").MakeGenericMethod(type);

                    gen.EmitConstant(ctx.ContextId);
                    gen.EmitConstant(_property.PropertyId);
                    Expr.Cast(Value, type).Emit(ctx, true);
                    gen.EmitCall(method);
                }
            }
            else
            {
                var nameInfo = Local ?? ctx.Scope.FindLocal(Identifier);
                if (nameInfo != null)
                {
                    if (nameInfo.IsClosured)
                        EmitSetClosured(ctx, nameInfo);
                    else
                        EmitSetLocal(ctx, nameInfo);
                }
            }
        }

        /// <summary>
        /// Assigns an ordinary local variable.
        /// </summary>
        private void EmitSetLocal(Context ctx, Local name)
        {
            var gen = ctx.CurrentMethod.Generator;

            var castNode = Expr.Cast(Value, name.Type);

            if (!name.IsRefArgument)
            {
                castNode.Emit(ctx, true);
                if (name.ArgumentId.HasValue)
                    gen.EmitSaveArgument(name.ArgumentId.Value);
                else
                    gen.EmitSaveLocal(name.LocalBuilder);
            }
            else
            {
                gen.EmitLoadArgument(name.ArgumentId.Value);
                castNode.Emit(ctx, true);
                gen.EmitSaveObject(name.Type);
            }
        }

        /// <summary>
        /// Assigns a closured variable in the closure it has been declared in.
        /// </summary>
        private void EmitSetClosured(Context ctx, Local name)
        {
            var gen = ctx.CurrentMethod.Generator;

            var closureType = ctx.Scope.EmitClosureInstance(ctx, name);

            Expr.Cast(Value, name.Type).Emit(ctx, true);

            var clsField = ctx.ResolveField(closureType, name.ClosureFieldName);
            gen.EmitSaveField(clsField.FieldInfo);
        }

        #endregion

        #region Debug

        protected bool Equals(SetIdentifierNode other)
        {
            return base.Equals(other) && Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((SetIdentifierNode) obj);
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
            return string.Format("set({0} = {1})", Identifier, Value);
        }

        #endregion
    }
}