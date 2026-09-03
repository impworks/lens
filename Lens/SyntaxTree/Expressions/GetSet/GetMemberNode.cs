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
        private TypeEntry _type;

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

        public bool RefArgumentRequired { get; set; }

        /// <summary>
        /// The list of type signatures if the given identifier is a method.
        /// </summary>
        public List<TypeSignature> TypeHints { get; set; }

        #endregion

        #region Binding results

        // an expression tree is built from the same members ordinary resolution settled on, so it
        // reads them off the node rather than resolving the access a second time

        internal FieldWrapper BoundField => _field;
        internal PropertyWrapper BoundProperty => _property;
        internal MethodWrapper BoundMethod => _method;
        internal TypeEntry BoundType => _type;
        internal bool IsStaticAccess => _isStatic;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            ResolveSelf(ctx);

            if (_type != null)
                CheckTypeInSafeMode(ctx, _type);

            CheckMemberInSafeMode(ctx, _field);
            CheckMemberInSafeMode(ctx, _property);
            CheckMemberInSafeMode(ctx, _method);

            // a rank > 1 array answers Length through Array.Length like any other property:
            // ldlen would read the bounds table instead of an element count
            if (Expression != null && Expression.Resolve(ctx).IsVectorArray && MemberName == "Length")
                return TypeEntryCache.Of<int>();

            if (_field != null)
                return _field.FieldType;

            // what may be passed by reference is decided here rather than while emitting, because
            // an editor binds the tree and never emits: a check that lives in EmitInternal is one
            // the reader of a half-written script never sees
            if (_property != null)
            {
                // a getter hands back a copy, and there is no storage behind it for a callee to
                // write into - unless it returns a managed pointer, which is a location and
                // nothing else
                if (_property.PropertyType.IsValueType && !_property.PropertyType.IsByRef && RefArgumentRequired)
                    Error(CompilerMessages.PropertyValuetypeRef, _property.DeclaringType.Materialize(), MemberName, _property.PropertyType.Materialize());

                return _property.PropertyType.Dereferenced();
            }

            // naming a method builds a delegate on the spot, which likewise has no storage
            if (RefArgumentRequired)
                Error(CompilerMessages.MethodRef);

            return _method.ReturnType.IsVoid()
                ? TypeEntryCache.Of(FunctionalHelper.CreateActionType(_method.ArgumentTypes.Select(x => x.Materialize()).ToArray()))
                : TypeEntryCache.Of(FunctionalHelper.CreateFuncType(_method.ReturnType.Materialize(), _method.ArgumentTypes.Select(x => x.Materialize()).ToArray()));
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

            _type = StaticTypeInfo != null
                    ? StaticTypeInfo
                    : (StaticType != null
                        ? ctx.ResolveType(StaticType)
                        : Expression.Resolve(ctx));

            // special case: array length
            if (_type.IsVectorArray && MemberName == "Length")
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
            var methods = ResolveMethods(ctx, argTypes);

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

        /// <summary>
        /// The methods of the receiver's type that the access could mean.
        ///
        /// A type declared by the script reports the absence of a name by throwing rather than by
        /// answering with an empty group, and a member that does not exist is an error to report
        /// against the access itself - not an exception to escape the binder with.
        /// </summary>
        private MethodWrapper[] ResolveMethods(Context ctx, TypeEntry[] argTypes)
        {
            try
            {
                return ctx.ResolveMethodGroup(_type, MemberName).Where(m => CheckMethodArgs(argTypes, m)).ToArray();
            }
            catch (KeyNotFoundException)
            {
                return new MethodWrapper[0];
            }
        }

        private static bool CheckMethodArgs(TypeEntry[] argTypes, MethodWrapper method)
        {
            if (argTypes.Length == 0)
                return true;

            if (method.ArgumentTypes.Length != argTypes.Length)
                return false;

            return !method.ArgumentTypes.Where((p, idx) => argTypes[idx] != null && p != argTypes[idx]).Any();
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            yield return new NodeChild(Expression);
        }

        // a static member has no expression to evaluate: the type is not a value
        internal override IReadOnlyList<NodeBase> Operands => Expression == null ? NoOperands : new[] {Expression};

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            var copy = Copy<GetMemberNode>();
            copy.Expression = operands[0];
            return copy;
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            if (!_isStatic)
            {
                Expression.EmitNodeForAccess(ctx);

                if (MemberName == "Length" && Expression.Resolve(ctx).IsVectorArray)
                {
                    gen.EmitGetArrayLength();
                    return;
                }
            }

            if (_field != null)
                EmitField(ctx, gen);

            else if (_property != null)
                EmitProperty(ctx, gen);

            if (_method != null)
                EmitMethod(ctx, gen);
        }

        /// <summary>
        /// Emits code for loading a field (possibly constant).
        /// </summary>
        private void EmitField(Context ctx, ILGenerator gen)
        {
            if (_field.IsLiteral)
            {
                var fieldType = _field.FieldType;
                var dataType = fieldType.IsEnum ? Enum.GetUnderlyingType(fieldType.Materialize()) : fieldType.Materialize();

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
                gen.EmitLoadField(_field.FieldInfo, ctx.IsPointerRequired(this) || RefArgumentRequired);
            }
        }

        /// <summary>
        /// Emits code for loading a property value.
        /// </summary>
        private void EmitProperty(Context ctx, ILGenerator gen)
        {
            gen.EmitCall(_property.Getter, _property.IsVirtual, _property.ConstrainedTo?.Materialize());

            // a getter that returns a managed pointer has left the location of the value on the
            // stack, which is what a caller asking for an address wants; anything else wants the
            // value at it
            if (_property.PropertyType.IsByRef)
            {
                if (!RefArgumentRequired && !ctx.IsPointerRequired(this))
                    gen.EmitLoadFromPointer(_property.PropertyType.ElementType.Materialize());

                return;
            }

            if (ctx.IsPointerRequired(this))
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
            if (_isStatic)
                gen.EmitNull();

            var retType = _method.ReturnType;
            var type = retType.IsVoid()
                ? FunctionalHelper.CreateActionType(_method.ArgumentTypes.Select(x => x.Materialize()).ToArray())
                : FunctionalHelper.CreateFuncType(retType.Materialize(), _method.ArgumentTypes.Select(x => x.Materialize()).ToArray());

            var ctor = ctx.ResolveConstructor(TypeEntryCache.Of(type), new[] {TypeEntryCache.Of<object>(), TypeEntryCache.Of<IntPtr>()});
            gen.EmitLoadFunctionPointer(_method.MethodInfo);
            gen.EmitCreateObject(ctor.ConstructorInfo);
        }

        #endregion

        #region Debug

        protected bool Equals(GetMemberNode other)
        {
            return base.Equals(other)
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