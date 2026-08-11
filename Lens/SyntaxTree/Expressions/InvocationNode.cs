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
        #region Binding

        /// <summary>
        /// What binding learned about this invocation.
        /// </summary>
        private class Binding : InvocationBinding
        {
            /// <summary>
            /// The method the invocation resolved to.
            /// </summary>
            public MethodWrapper Method;

            /// <summary>
            /// The expression to invoke the method on. Null for functions and static methods.
            /// </summary>
            public NodeBase InvocationSource;

            /// <summary>
            /// The resolved type hints of a generic method or delegate, if any were given.
            /// </summary>
            public TypeEntry[] TypeHints;
        }

        protected override InvocationBinding GetBinding(Context ctx)
        {
            return ctx.BindingOf<Binding>(this);
        }

        protected override CallableWrapperBase GetWrapper(Context ctx)
        {
            return ctx.BindingOf<Binding>(this).Method;
        }

        #endregion

        #region Fields

        /// <summary>
        /// Entire invokable expression, like:
        /// myFunc
        /// type::myMethod
        /// obj.myMethod
        /// </summary>
        public NodeBase Expression { get; set; }

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            // resolve the argument types
            base.ResolveInternal(ctx, mustReturn);

            var binding = ctx.BindingOf<Binding>(this);

            if (Expression is GetMemberNode)
                ResolveGetMember(ctx, binding, Expression as GetMemberNode);
            else if (Expression is GetIdentifierNode)
                ResolveGetIdentifier(ctx, binding, Expression as GetIdentifierNode);
            else
                ResolveExpression(ctx, binding, Expression);

            ApplyLambdaArgTypes(ctx);

            return ResolvePartial(binding.Method, binding.Method.ReturnType, binding.ArgTypes);
        }

        /// <summary>
        /// Resolves the method if the expression was a member getter (obj.field or type::field).
        /// </summary>
        private void ResolveGetMember(Context ctx, Binding binding, GetMemberNode node)
        {
            binding.InvocationSource = node.Expression;
            var type = binding.InvocationSource != null
                ? binding.InvocationSource.Resolve(ctx)
                : ctx.ResolveType(node.StaticType);

            CheckTypeInSafeMode(ctx, type);

            if (node.TypeHints != null && node.TypeHints.Count > 0)
                binding.TypeHints = node.TypeHints.Select(x => ctx.ResolveType(x, true)).ToArray();

            try
            {
                // resolve a normal method
                try
                {
                    binding.Method = ctx.ResolveMethod(
                        type.Materialize(),
                        node.MemberName,
                        TypeEntry.Materialize(binding.ArgTypes),
                        TypeEntry.Materialize(binding.TypeHints),
                        (idx, types) => ctx.ResolveLambda(binding.Arguments[idx] as LambdaNode, types)
                    );

                    if (binding.Method.IsStatic)
                        binding.InvocationSource = null;

                    return;
                }
                catch (KeyNotFoundException)
                {
                    if (binding.InvocationSource == null)
                        throw;
                }

                // resolve a callable field
                try
                {
                    ctx.ResolveField(type.Materialize(), node.MemberName);
                    ResolveExpression(ctx, binding, node);
                    return;
                }
                catch (KeyNotFoundException)
                {
                }

                // resolve a callable property
                try
                {
                    ctx.ResolveProperty(type.Materialize(), node.MemberName);
                    ResolveExpression(ctx, binding, node);
                    return;
                }
                catch (KeyNotFoundException)
                {
                }

                // the call is an extension method call after all: the receiver becomes argument
                // zero, which is a binding result and does not touch the node's own argument list
                binding.Arguments = (binding.Arguments[0] is UnitNode)
                    ? new List<NodeBase> {binding.InvocationSource}
                    : new[] {binding.InvocationSource}.Union(binding.Arguments).ToList();

                var oldArgTypes = binding.ArgTypes;
                binding.ArgTypes = binding.Arguments.Select(a => a.Resolve(ctx)).ToArray();
                binding.InvocationSource = null;

                try
                {
                    // resolve a local function that is implicitly used as an extension method
                    binding.Method = ctx.ResolveMethod(
                        ctx.MainType.TypeInfo,
                        node.MemberName,
                        TypeEntry.Materialize(binding.ArgTypes),
                        resolver: (idx, types) => ctx.ResolveLambda(binding.Arguments[idx] as LambdaNode, types)
                    );

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

                    binding.Method = ctx.ResolveExtensionMethod(
                        type.Materialize(),
                        node.MemberName,
                        TypeEntry.Materialize(oldArgTypes),
                        TypeEntry.Materialize(binding.TypeHints),
                        (idx, types) => ctx.ResolveLambda(binding.Arguments[idx] as LambdaNode, types)
                    );
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
        private void ResolveGetIdentifier(Context ctx, Binding binding, GetIdentifierNode node)
        {
            // local
            var nameInfo = ctx.Scope.FindLocal(node.Identifier);
            if (nameInfo != null)
            {
                ResolveExpression(ctx, binding, node);
                return;
            }

            if (node.TypeHints != null && node.TypeHints.Count > 0)
                binding.TypeHints = node.TypeHints.Select(x => ctx.ResolveType(x, true)).ToArray();

            // function
            try
            {
                binding.Method = ctx.ResolveMethod(
                    ctx.MainType.TypeInfo,
                    node.Identifier,
                    TypeEntry.Materialize(binding.ArgTypes),
                    TypeEntry.Materialize(binding.TypeHints),
                    (idx, types) => ctx.ResolveLambda(binding.Arguments[idx] as LambdaNode, types)
                );

                if (binding.Method == null)
                    throw new KeyNotFoundException();

                if (binding.ArgTypes.Length == 0 && node.Identifier.IsAnyOf(EntityNames.RunMethodName, EntityNames.EntryPointMethodName))
                    Error(CompilerMessages.ReservedFunctionInvocation, node.Identifier);

                return;
            }
            catch (AmbiguousMatchException)
            {
                Error(CompilerMessages.FunctionInvocationAmbiguous, node.Identifier);
            }
            catch (TypeMatchException ex)
            {
                Error(ex.Message, node);
            }
            catch (KeyNotFoundException)
            {
            }

            // global property with a delegate
            try
            {
                ctx.ResolveGlobalProperty(node.Identifier);
                ResolveExpression(ctx, binding, node);
            }
            catch (KeyNotFoundException)
            {
                Error(CompilerMessages.FunctionNotFound, node.Identifier);
            }
        }

        /// <summary>
        /// Resolves a method from the expression, considering it an instance of a delegate type.
        /// </summary>
        private void ResolveExpression(Context ctx, Binding binding, NodeBase node)
        {
            var exprType = node.Resolve(ctx);
            if (!exprType.IsCallableType())
                Error(CompilerMessages.TypeNotCallable, exprType);

            try
            {
                // argtypes are required for partial application
                binding.Method = ctx.ResolveMethod(exprType.Materialize(), "Invoke", TypeEntry.Materialize(binding.ArgTypes));
            }
            catch (KeyNotFoundException)
            {
                // delegate argument types are mismatched:
                // infer whatever method there is and detect actual error
                binding.Method = ctx.ResolveMethod(exprType.Materialize(), "Invoke");

                var argTypes = binding.Method.ArgumentTypes;
                if (argTypes.Length != binding.ArgTypes.Length)
                    Error(CompilerMessages.DelegateArgumentsCountMismatch, exprType, argTypes.Length, binding.ArgTypes.Length);

                for (var idx = 0; idx < argTypes.Length; idx++)
                {
                    var fromType = binding.ArgTypes[idx];
                    var toType = argTypes[idx];
                    if (!toType.IsExtendablyAssignableFrom(ctx.Resolver, fromType))
                        Error(binding.Arguments[idx], CompilerMessages.ArgumentTypeMismatch, fromType, toType);
                }
            }

            binding.InvocationSource = node;
        }

        #endregion

        #region Transform

        protected override IEnumerable<NodeChild> GetChildren()
        {
            if (Expression is GetMemberNode)
            {
                var getMbr = Expression as GetMemberNode;
                if (getMbr.Expression != null)
                    yield return new NodeChild(getMbr.Expression);
            }
            else if (!(Expression is GetIdentifierNode))
            {
                yield return new NodeChild(Expression);
            }

            foreach (var curr in base.GetChildren())
                yield return curr;
        }

        #endregion

        #region Closures

        // the invokable expression is not among this node's children when it is an identifier or a
        // member access, so it has to be walked explicitly or the name it mentions goes unnoticed

        public override void AnalyzeClosures(Context ctx)
        {
            if (Expression is GetIdentifierNode || Expression is GetMemberNode)
                Expression.AnalyzeClosures(ctx);

            base.AnalyzeClosures(ctx);
        }

        public override void EmitClosureEntities(Context ctx)
        {
            if (Expression is GetIdentifierNode || Expression is GetMemberNode)
                Expression.EmitClosureEntities(ctx);

            base.EmitClosureEntities(ctx);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            var binding = ctx.BindingOf<Binding>(this);

            binding.InvocationSource?.EmitNodeForAccess(ctx);

            if (binding.ArgTypes.Length > 0)
            {
                var destTypes = binding.Method.ArgumentTypes;
                for (var idx = 0; idx < binding.Arguments.Count; idx++)
                {
                    var arg = binding.Arguments[idx];
                    var argRef = arg is IPointerProvider && (arg as IPointerProvider).RefArgumentRequired;
                    var targetRef = destTypes[idx].IsByRef;

                    if (argRef != targetRef)
                    {
                        if (argRef)
                            Error(arg, CompilerMessages.ReferenceArgUnexpected);
                        else
                            Error(arg, CompilerMessages.ReferenceArgExpected, idx + 1, destTypes[idx].Materialize().GetElementType());
                    }

                    var expr = argRef ? arg : Expr.Cast(arg, destTypes[idx].Materialize());
                    expr.Emit(ctx, true);
                }
            }

            var sourceType = binding.InvocationSource?.Resolve(ctx);
            var isVirt = sourceType is { IsValueType: false };
            gen.EmitCall(binding.Method.MethodInfo, isVirt);
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
