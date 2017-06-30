using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.SyntaxTree.Literals;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
    /// <summary>
    /// A node representing a method being invoked.
    /// </summary>
    internal class InvocationNode : InvocationNodeBase
    {
        #region Fields

        /// <summary>
        /// Entire invokable expression, like:
        /// myFunc
        /// type::myMethod
        /// obj.myMethod
        /// </summary>
        public NodeBase Expression { get; set; }

        /// <summary>
        /// Expression to invoke the method on, if any.
        /// Is null for functions or static methods.
        /// </summary>
        private NodeBase _invocationSource;

        /// <summary>
        /// Invoked method wrapper.
        /// </summary>
        private MethodWrapper _method;

        /// <summary>
        /// Generic wrapper implementation for base class interface (used for partial application, etc).
        /// </summary>
        protected override CallableWrapperBase Wrapper => _method;

        /// <summary>
        /// Optional type hints for generic methods or delegates.
        /// </summary>
        private Type[] _typeHints;

        /// <summary>
        /// The flag indicating that current method has been resolved as static,
        /// accepting the base object as first parameter.
        /// </summary>
        private bool _isExtensionMethod;

        #endregion

        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            // resolve _ArgTypes
            base.ResolveInternal(ctx, mustReturn);

            if (Expression is GetMemberNode)
                ResolveGetMember(ctx, Expression as GetMemberNode);
            else if (Expression is GetIdentifierNode)
                ResolveGetIdentifier(ctx, Expression as GetIdentifierNode);
            else
                ResolveExpression(ctx, Expression);

            ApplyLambdaArgTypes(ctx);

            return ResolvePartial(_method, _method.ReturnType, ArgTypes);
        }

        /// <summary>
        /// Resolves the method if the expression was a member getter (obj.field or type::field).
        /// </summary>
        private void ResolveGetMember(Context ctx, GetMemberNode node)
        {
            _invocationSource = node.Expression;
            var type = _invocationSource != null
                ? _invocationSource.Resolve(ctx)
                : ctx.ResolveType(node.StaticType);

            CheckTypeInSafeMode(ctx, type);

            if (node.TypeHints != null && node.TypeHints.Count > 0)
                _typeHints = node.TypeHints.Select(x => ctx.ResolveType(x, true)).ToArray();

            try
            {
                // resolve a normal method
                try
                {
                    _method = ctx.ResolveMethod(
                        type,
                        node.MemberName,
                        ArgTypes,
                        _typeHints,
                        (idx, types) => ctx.ResolveLambda(Arguments[idx] as LambdaNode, types)
                    );

                    if (_method.IsStatic)
                        _invocationSource = null;

                    return;
                }
                catch (KeyNotFoundException)
                {
                    if (_invocationSource == null)
                        throw;
                }

                // resolve a callable field
                try
                {
                    ctx.ResolveField(type, node.MemberName);
                    ResolveExpression(ctx, node);
                    return;
                }
                catch (KeyNotFoundException)
                {
                }

                // resolve a callable property
                try
                {
                    ctx.ResolveProperty(type, node.MemberName);
                    ResolveExpression(ctx, node);
                    return;
                }
                catch (KeyNotFoundException)
                {
                }

                Arguments = (Arguments[0] is UnitNode)
                    ? new List<NodeBase> {_invocationSource}
                    : new[] {_invocationSource}.Union(Arguments).ToList();

                var oldArgTypes = ArgTypes;
                ArgTypes = Arguments.Select(a => a.Resolve(ctx)).ToArray();
                _invocationSource = null;

                try
                {
                    // resolve a local function that is implicitly used as an extension method
                    _method = ctx.ResolveMethod(
                        ctx.MainType.TypeInfo,
                        node.MemberName,
                        ArgTypes,
                        resolver: (idx, types) => ctx.ResolveLambda(Arguments[idx] as LambdaNode, types)
                    );

                    _isExtensionMethod = true;
                    return;
                }
                catch (KeyNotFoundException)
                {
                }

                // resolve a declared extension method
                // most time-consuming operation, therefore is last checked
                try
                {
                    if (!ctx.Options.AllowExtensionMethods)
                        throw new KeyNotFoundException();

                    _method = ctx.ResolveExtensionMethod(
                        type,
                        node.MemberName,
                        oldArgTypes,
                        _typeHints,
                        (idx, types) => ctx.ResolveLambda(Arguments[idx] as LambdaNode, types)
                    );

                    _isExtensionMethod = true;
                }
                catch (KeyNotFoundException)
                {
                    var msg = node.StaticType != null
                        ? CompilerMessages.TypeStaticMethodNotFound
                        : CompilerMessages.TypeMethodNotFound;

                    Error(msg, type, node.MemberName);
                }
            }
            catch (AmbiguousMatchException)
            {
                Error(CompilerMessages.TypeMethodInvocationAmbiguous, type, node.MemberName);
            }
        }

        /// <summary>
        /// Resolves the method as a global function, imported property or a local variable with a delegate.
        /// </summary>
        private void ResolveGetIdentifier(Context ctx, GetIdentifierNode node)
        {
            // local
            var nameInfo = ctx.Scope.FindLocal(node.Identifier);
            if (nameInfo != null)
            {
                ResolveExpression(ctx, node);
                return;
            }

            // function
            try
            {
                _method = ctx.ResolveMethod(
                    ctx.MainType.TypeInfo,
                    node.Identifier,
                    ArgTypes,
                    resolver: (idx, types) => ctx.ResolveLambda(Arguments[idx] as LambdaNode, types)
                );

                if (_method == null)
                    throw new KeyNotFoundException();

                if (ArgTypes.Length == 0 && node.Identifier.IsAnyOf(EntityNames.RunMethodName, EntityNames.EntryPointMethodName))
                    Error(CompilerMessages.ReservedFunctionInvocation, node.Identifier);

                return;
            }
            catch (AmbiguousMatchException)
            {
                Error(CompilerMessages.FunctionInvocationAmbiguous, node.Identifier);
            }
            catch (KeyNotFoundException)
            {
            }

            // global property with a delegate
            try
            {
                ctx.ResolveGlobalProperty(node.Identifier);
                ResolveExpression(ctx, node);
            }
            catch (KeyNotFoundException)
            {
                Error(CompilerMessages.FunctionNotFound, node.Identifier);
            }
        }

        /// <summary>
        /// Resolves a method from the expression, considering it an instance of a delegate type.
        /// </summary>
        private void ResolveExpression(Context ctx, NodeBase node)
        {
            var exprType = node.Resolve(ctx);
            if (!exprType.IsCallableType())
                Error(CompilerMessages.TypeNotCallable, exprType);

            try
            {
                // argtypes are required for partial application
                _method = ctx.ResolveMethod(exprType, "Invoke", ArgTypes);
            }
            catch (KeyNotFoundException)
            {
                // delegate argument types are mismatched:
                // infer whatever method there is and detect actual error
                _method = ctx.ResolveMethod(exprType, "Invoke");

                var argTypes = _method.ArgumentTypes;
                if (argTypes.Length != ArgTypes.Length)
                    Error(CompilerMessages.DelegateArgumentsCountMismatch, exprType, argTypes.Length, ArgTypes.Length);

                for (var idx = 0; idx < argTypes.Length; idx++)
                {
                    var fromType = ArgTypes[idx];
                    var toType = argTypes[idx];
                    if (!toType.IsExtendablyAssignableFrom(fromType))
                        Error(Arguments[idx], CompilerMessages.ArgumentTypeMismatch, fromType, toType);
                }
            }

            _invocationSource = node;
        }

        #endregion

        #region Transform

        protected override IEnumerable<NodeChild> GetChildren()
        {
            if (Expression is GetMemberNode)
            {
                // epic kludge: had to reset previously cached InvocationSource if it is expanded
                var getMbr = Expression as GetMemberNode;
                if (getMbr.Expression != null)
                    yield return new NodeChild(getMbr.Expression, x => getMbr.Expression = _invocationSource = x);
            }
            else if (!(Expression is GetIdentifierNode))
            {
                yield return new NodeChild(Expression, x => Expression = x);
            }

            foreach (var curr in base.GetChildren())
                yield return curr;
        }

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            var getMember = Expression as GetMemberNode;
            if (getMember?.IsSafeNavigation == true)
            {
                var type = Resolve(ctx, mustReturn);
                var local = ctx.Scope.DeclareImplicit(ctx, getMember.Expression.Resolve(ctx), false);
                var args = Arguments.AsEnumerable();
                if (_isExtensionMethod) args = args.Skip(1);

                return Expr.Block(
                    Expr.Set(local, getMember.Expression),
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
                                Expr.Invoke(
                                    Expr.GetMember(
                                        Expr.Get(local),
                                        getMember.MemberName,
                                        getMember.TypeHints.ToArray()
                                    ),
                                    args.ToArray()
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

        #region Process closures

        public override void ProcessClosures(Context ctx)
        {
            if (Expression is GetIdentifierNode || Expression is GetMemberNode)
                Expression.ProcessClosures(ctx);

            base.ProcessClosures(ctx);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            if (_invocationSource != null)
            {
                _invocationSource.EmitNodeForAccess(ctx);
            }

            if (ArgTypes.Length > 0)
            {
                var destTypes = _method.ArgumentTypes;
                for (var idx = 0; idx < Arguments.Count; idx++)
                {
                    var arg = Arguments[idx];
                    var argRef = arg is IPointerProvider && (arg as IPointerProvider).RefArgumentRequired;
                    var targetRef = destTypes[idx].IsByRef;

                    if (argRef != targetRef)
                    {
                        if (argRef)
                            Error(arg, CompilerMessages.ReferenceArgUnexpected);
                        else
                            Error(arg, CompilerMessages.ReferenceArgExpected, idx + 1, destTypes[idx].GetElementType());
                    }

                    var expr = argRef ? Arguments[idx] : Expr.Cast(Arguments[idx], destTypes[idx]);
                    expr.Emit(ctx, true);
                }
            }

            var isVirt = _invocationSource != null && _invocationSource.Resolve(ctx).IsClass;
            gen.EmitCall(_method.MethodInfo, isVirt);
        }

        #endregion

        #region Helpers

        protected override InvocationNodeBase RecreateSelfWithArgs(IEnumerable<NodeBase> newArgs)
        {
            return new InvocationNode {Expression = Expression, Arguments = newArgs.ToList()};
        }

        #endregion

        #region Debug

        protected bool Equals(InvocationNode other)
        {
            return base.Equals(other)
                   && Equals(Expression, other.Expression);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((InvocationNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (Expression != null ? Expression.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return string.Format("invoke({0}, args: {1})", Expression, string.Join(",", Arguments));
        }

        #endregion
    }
}