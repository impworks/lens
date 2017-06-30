using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.GetSet
{
    /// <summary>
    /// A node representing read access to a member of a type, either field or property.
    /// </summary>
    internal class GetMemberNode : MemberNodeBase, IPointerProvider
    {
        #region Constructor

        public GetMemberNode()
        {
            TypeHints = new List<TypeSignature>();
        }

        #endregion

        #region Fields

        /// <summary>
        /// Type (for static member access).
        /// </summary>
        private Type _type;

        /// <summary>
        /// Cached field reference (if the member resolves to it).
        /// </summary>
        private FieldWrapper _field;

        /// <summary>
        /// Cached method reference (if the member resolves to it).
        /// </summary>
        private MethodWrapper _method;

        /// <summary>
        /// Cached property reference (if the member resolves to it).
        /// </summary>
        private PropertyWrapper _property;

        /// <summary>
        /// Flag indicating that the access is to a static member of the type.
        /// </summary>
        private bool _isStatic;

        public bool PointerRequired { get; set; }
        public bool RefArgumentRequired { get; set; }

        /// <summary>
        /// The list of type signatures if the given identifier is a method.
        /// </summary>
        public List<TypeSignature> TypeHints { get; set; }

        #endregion

        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            ResolveSelf(ctx);

            if (_type != null)
                CheckTypeInSafeMode(ctx, _type);

            Type result;

            if (Expression != null && Expression.Resolve(ctx).IsArray && MemberName == "Length")
                result = typeof(int);

            else if (_field != null)
                result = _field.FieldType;

            else if (_property != null)
                result = _property.PropertyType;

            else
                result = _method.ReturnType.IsVoid()
                    ? FunctionalHelper.CreateActionType(_method.ArgumentTypes)
                    : FunctionalHelper.CreateFuncType(_method.ReturnType, _method.ArgumentTypes);

            return IsSafeNavigation && result.IsValueType && !result.IsNullableType()
                   ? result.MakeNullableType()
                   : result;
        }

        /// <summary>
        /// Attempts to resolve current node and sets either of the following fields:
        /// _Field, _Method, _Property
        /// 
        /// The following fields are also set:
        /// _Type, _Static
        /// </summary>
        private void ResolveSelf(Context ctx)
        {
            void check()
            {
                if (Expression == null && !_isStatic)
                    Error(CompilerMessages.DynamicMemberFromStaticContext, _type, MemberName);

                if (_method == null && TypeHints.Count > 0)
                    Error(CompilerMessages.TypeArgumentsForNonMethod, _type, MemberName);
            }

            _type = StaticType != null
                ? ctx.ResolveType(StaticType)
                : Expression.Resolve(ctx);

            // special case: array length
            if (_type.IsArray && MemberName == "Length")
            {
                check();
                return;
            }

            // check for field
            try
            {
                _field = ctx.ResolveField(_type, MemberName);
                _isStatic = _field.IsStatic;

                check();
                return;
            }
            catch (KeyNotFoundException)
            {
            }

            // check for property
            try
            {
                _property = ctx.ResolveProperty(_type, MemberName);

                if (!_property.CanGet)
                    Error(CompilerMessages.PropertyNoGetter, _type, MemberName);

                _isStatic = _property.IsStatic;

                check();
                return;
            }
            catch (KeyNotFoundException)
            {
            }

            // check for event: events are only allowed at the left side of += and -=
            try
            {
                ctx.ResolveEvent(_type, MemberName);
                Error(CompilerMessages.EventAsExpr);
            }
            catch (KeyNotFoundException)
            {
            }

            // find method
            var argTypes = TypeHints.Select(t => t.FullSignature == "_" ? null : ctx.ResolveType(t)).ToArray();
            var methods = ctx.ResolveMethodGroup(_type, MemberName).Where(m => CheckMethodArgs(argTypes, m)).ToArray();

            if (methods.Length == 0)
                Error(argTypes.Length == 0 ? CompilerMessages.TypeIdentifierNotFound : CompilerMessages.TypeMethodNotFound, _type.Name, MemberName);

            if (methods.Length > 1)
                Error(CompilerMessages.TypeMethodAmbiguous, _type.Name, MemberName);

            _method = methods[0];
            if (_method.ArgumentTypes.Length > 16)
                Error(CompilerMessages.CallableTooManyArguments);

            _isStatic = _method.IsStatic;

            check();
        }

        private static bool CheckMethodArgs(Type[] argTypes, MethodWrapper method)
        {
            if (argTypes.Length == 0)
                return true;

            if (method.ArgumentTypes.Length != argTypes.Length)
                return false;

            return !method.ArgumentTypes.Where((p, idx) => argTypes[idx] != null && p != argTypes[idx]).Any();
        }

        #endregion

        #region Transform

        protected override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Expression, x => Expression = x);
        }

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            if (IsSafeNavigation)
            {
                if (Expression == null)
                    throw new ArgumentNullException(nameof(Expression));

                var type = Resolve(ctx, mustReturn);
                var local = ctx.Scope.DeclareImplicit(ctx, Expression.Resolve(ctx), false);
                return Expr.Block(
                    Expr.Set(local, Expression),
                    Expr.If(
                        Expr.Equal(
                            Expr.Get(local),
                            Expr.Null()
                        ),
                        Expr.Block(
                            Expr.Default(type)
                        ),
                        Expr.Block(
                            Expr.Cast(
                                Expr.GetMember(
                                    Expr.Get(local),
                                    MemberName,
                                    TypeHints.ToArray()
                                ),
                                type
                            )
                        )
                    )
                );
            }

            return base.Expand(ctx, mustReturn);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            if (!_isStatic)
            {
                Expression.EmitNodeForAccess(ctx);

                if (MemberName == "Length" && Expression.Resolve(ctx).IsArray)
                {
                    gen.EmitGetArrayLength();
                    return;
                }
            }

            if (_field != null)
                EmitField(gen);

            else if (_property != null)
                EmitProperty(ctx, gen);

            if (_method != null)
                EmitMethod(ctx, gen);
        }

        /// <summary>
        /// Emits code for loading a field (possibly constant).
        /// </summary>
        private void EmitField(ILGenerator gen)
        {
            if (_field.IsLiteral)
            {
                var fieldType = _field.FieldType;
                var dataType = fieldType.IsEnum ? Enum.GetUnderlyingType(fieldType) : fieldType;

                var value = _field.FieldInfo.GetValue(null);

                if (dataType == typeof(int))
                    gen.EmitConstant((int) value);
                else if (dataType == typeof(long))
                    gen.EmitConstant((long) value);
                else if (dataType == typeof(double))
                    gen.EmitConstant((double) value);
                else if (dataType == typeof(float))
                    gen.EmitConstant((float) value);

                else if (dataType == typeof(uint))
                    gen.EmitConstant(unchecked((int) (uint) value));
                else if (dataType == typeof(ulong))
                    gen.EmitConstant(unchecked((long) (ulong) value));

                else if (dataType == typeof(byte))
                    gen.EmitConstant((byte) value);
                else if (dataType == typeof(sbyte))
                    gen.EmitConstant((sbyte) value);
                else if (dataType == typeof(short))
                    gen.EmitConstant((short) value);
                else if (dataType == typeof(ushort))
                    gen.EmitConstant((ushort) value);
                else if (dataType == typeof(string))
                    gen.EmitConstant((string) value);
                else
                    throw new NotImplementedException("Unknown literal field type!");
            }
            else
            {
                gen.EmitLoadField(_field.FieldInfo, PointerRequired || RefArgumentRequired);
            }
        }

        /// <summary>
        /// Emits code for loading a property value.
        /// </summary>
        private void EmitProperty(Context ctx, ILGenerator gen)
        {
            if (_property.PropertyType.IsValueType && RefArgumentRequired)
                Error(CompilerMessages.PropertyValuetypeRef, _property.Type, MemberName, _property.PropertyType);

            gen.EmitCall(_property.Getter, _property.IsVirtual);

            if (PointerRequired)
            {
                var tmpVar = ctx.Scope.DeclareImplicit(ctx, _property.PropertyType, false);
                gen.EmitSaveLocal(tmpVar.LocalBuilder);
                gen.EmitLoadLocal(tmpVar.LocalBuilder, true);
            }
        }

        /// <summary>
        /// Emits code for getting the method as a delegate instance.
        /// </summary>
        private void EmitMethod(Context ctx, ILGenerator gen)
        {
            if (RefArgumentRequired)
                Error(CompilerMessages.MethodRef);

            if (_isStatic)
                gen.EmitNull();

            var retType = _method.ReturnType;
            var type = retType.IsVoid()
                ? FunctionalHelper.CreateActionType(_method.ArgumentTypes)
                : FunctionalHelper.CreateFuncType(retType, _method.ArgumentTypes);

            var ctor = ctx.ResolveConstructor(type, new[] {typeof(object), typeof(IntPtr)});
            gen.EmitLoadFunctionPointer(_method.MethodInfo);
            gen.EmitCreateObject(ctor.ConstructorInfo);
        }

        #endregion

        #region Debug

        protected bool Equals(GetMemberNode other)
        {
            return base.Equals(other)
                   && PointerRequired.Equals(other.PointerRequired)
                   && RefArgumentRequired.Equals(other.RefArgumentRequired)
                   && TypeHints.SequenceEqual(other.TypeHints);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((GetMemberNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ PointerRequired.GetHashCode();
                hashCode = (hashCode * 397) ^ RefArgumentRequired.GetHashCode();
                hashCode = (hashCode * 397) ^ (TypeHints != null ? TypeHints.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            var typehints = TypeHints.Any() ? "<" + string.Join(", ", TypeHints) + ">" : string.Empty;
            return StaticType == null
                ? string.Format("getmbr({0}{1} of value {2})", MemberName, typehints, Expression)
                : string.Format("getmbr({0}{1} of type {2})", MemberName, typehints, StaticType);
        }

        #endregion
    }
}