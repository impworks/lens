using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
    /// <summary>
    /// A base class for various forms of method invocation that stores arguments.
    /// </summary>
    internal abstract class InvocationNodeBase : NodeBase
    {
        #region Constructor

        protected InvocationNodeBase()
        {
            Arguments = new List<NodeBase>();
        }

        #endregion

        #region Binding

        /// <summary>
        /// What binding learned about an invocation. Lives in a side table on the context, so that
        /// the node itself remains a description of the source and nothing else.
        /// </summary>
        protected class InvocationBinding
        {
            /// <summary>
            /// The argument expressions binding settled on.
            ///
            /// Equals the node's own argument list, except when a method call turns out to be an
            /// extension method call: the receiver then becomes argument zero.
            /// </summary>
            public List<NodeBase> Arguments;

            /// <summary>
            /// The resolved types of those arguments.
            /// </summary>
            public TypeEntry[] ArgTypes;
        }

        /// <summary>
        /// Returns this invocation's binding record.
        /// </summary>
        protected abstract InvocationBinding GetBinding(Context ctx);

        /// <summary>
        /// Returns the callable that binding resolved this invocation to.
        /// </summary>
        protected abstract CallableWrapperBase GetWrapper(Context ctx);

        // an expression tree is built from the callable binding already settled on: re-resolving it
        // would drop the receiver-as-argument-zero rewrite an extension method call went through

        internal CallableWrapperBase BoundCallable(Context ctx) => GetWrapper(ctx);
        internal List<NodeBase> BoundArguments(Context ctx) => GetBinding(ctx).Arguments;

        #endregion

        #region Fields

        /// <summary>
        /// Passed argument expressions, exactly as they were parsed.
        /// </summary>
        public List<NodeBase> Arguments { get; set; }

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            TypeEntry TypeOf(NodeBase arg)
            {
                var gin = arg as GetIdentifierNode;
                if (gin != null && gin.Identifier == "_")
                    return TypeEntryCache.Of<UnspecifiedType>();

                return arg.Resolve(ctx);
            }

            var binding = GetBinding(ctx);
            binding.Arguments = Arguments;
            binding.ArgTypes = Arguments.Select(TypeOf).ToArray();

            // discard 'unit' pseudoargument
            if (binding.ArgTypes.Length == 1 && binding.ArgTypes[0].Is<UnitType>())
                binding.ArgTypes = new TypeEntry[0];

            // prepares arguments only
            return null;
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            for (var idx = 0; idx < Arguments.Count; idx++)
            {
                var identifier = Arguments[idx] as GetIdentifierNode;
                var isPartialArg = identifier != null && identifier.Identifier == "_";
                if (!isPartialArg)
                    yield return new NodeChild(Arguments[idx]);
            }
        }

        internal override IReadOnlyList<NodeBase> Operands => Arguments;

        /// <summary>
        /// A partial application's placeholder stands for an argument that is not being passed at
        /// all, so there is nothing about it to evaluate early.
        /// </summary>
        internal override bool CanHoistOperand(int index)
        {
            return !(Arguments[index] is GetIdentifierNode identifier && identifier.Identifier == "_");
        }

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            var copy = Copy<InvocationNodeBase>();
            copy.Arguments = operands.ToList();
            return copy;
        }

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            var binding = GetBinding(ctx);
            var wrapper = GetWrapper(ctx);

            if (wrapper.IsPartiallyApplied)
            {
                // (expr) _ a b _
                // is transformed into
                // (pa0:T1 pa1:T2) -> (expr) (pa0) (a) (b) (pa1)
                var argDefs = new List<FunctionArgument>();
                var argExprs = new List<NodeBase>();
                for (var idx = 0; idx < binding.ArgTypes.Length; idx++)
                {
                    if (binding.ArgTypes[idx].Is<UnspecifiedType>())
                    {
                        var argName = ctx.Unique.AnonymousArgName();
                        argDefs.Add(Expr.Arg(argName, wrapper.ArgumentTypes[idx].FullName));
                        argExprs.Add(Expr.Get(argName));
                    }
                    else
                    {
                        argExprs.Add(binding.Arguments[idx]);
                    }
                }

                return Expr.Lambda(argDefs, RecreateSelfWithArgs(argExprs));
            }

            if (wrapper.IsVariadic)
            {
                var srcTypes = binding.ArgTypes;
                var dstTypes = wrapper.ArgumentTypes;
                var lastDst = dstTypes[dstTypes.Length - 1];
                var fixedCount = dstTypes.Length - 1;

                // the call may already pass the array itself, in which case there is nothing to pack
                var isPacked = srcTypes.Length == dstTypes.Length && srcTypes[srcTypes.Length - 1] == lastDst;

                // compress items into an array:
                //     fx a b c d
                // becomes
                //     fx a b (new[ c as X; d as X ])
                if (!isPacked)
                {
                    var elemType = lastDst.ElementType;

                    // an argument list of exactly one unit is the 'no arguments at all' spelling, and
                    // resolution has already dropped it from the argument types
                    var args = srcTypes.Length == 0 ? new List<NodeBase>() : binding.Arguments;
                    var simpleArgs = args.Take(fixedCount);
                    var variadicArgs = args.Skip(fixedCount).Select(x => (NodeBase) Expr.CastTransparent(x, elemType)).ToArray();

                    // an array initializer cannot express an empty array: the variadic tail of a call
                    // that passes nothing for it has to be constructed explicitly
                    var combined = variadicArgs.Length == 0
                        ? (NodeBase) Expr.Array(elemType, Expr.Int(0))
                        : Expr.Array(variadicArgs);

                    return RecreateSelfWithArgs(simpleArgs.Concat(new[] {combined}));
                }
            }

            return base.Expand(ctx, mustReturn);
        }

        /// <summary>
        /// Creates a similar instance of invocation node descendant with replaced arguments list.
        /// </summary>
        protected abstract InvocationNodeBase RecreateSelfWithArgs(IEnumerable<NodeBase> newArgs);

        #endregion

        #region Helpers

        /// <summary>
        /// Resolves the expression type in case of partial application.
        /// </summary>
        protected static TypeEntry ResolvePartial(CallableWrapperBase wrapper, TypeEntry returnType, TypeEntry[] argTypes)
        {
            if (!wrapper.IsPartiallyApplied)
                return returnType;

            var lambdaArgTypes = new List<Type>();
            for (var idx = 0; idx < argTypes.Length; idx++)
            {
                if (argTypes[idx].Is<UnspecifiedType>())
                    lambdaArgTypes.Add(wrapper.ArgumentTypes[idx].Materialize());
            }

            return TypeEntryCache.Of(FunctionalHelper.CreateDelegateType(returnType.Materialize(), lambdaArgTypes.ToArray()));
        }

        /// <summary>
        /// Apply inferred types to untyped lambda arguments.
        /// </summary>
        protected void ApplyLambdaArgTypes(Context ctx)
        {
            var binding = GetBinding(ctx);
            var expectedTypes = GetWrapper(ctx).ArgumentTypes;

            var count = Math.Min(binding.ArgTypes.Length, Math.Min(binding.Arguments.Count, expectedTypes.Length));
            for (var idx = 0; idx < count; idx++)
            {
                var expected = expectedTypes[idx];
                var lambda = binding.Arguments[idx] as LambdaNode;

                // only a lambda can become an expression tree: a delegate value has no body left to
                // walk by the time it reaches the call
                if (expected.IsExpressionType() && lambda == null)
                {
                    var passed = binding.ArgTypes[idx];
                    Error(
                        binding.Arguments[idx],
                        passed.IsCallableType() ? CompilerMessages.ExpressionTreeNoDelegateValue : CompilerMessages.ExpressionTreeLambdaRequired,
                        passed.IsCallableType() ? passed.FullName : expected.FullName
                    );
                }

                if (lambda == null || !(binding.ArgTypes[idx].IsLambdaType() || expected.IsExpressionType()))
                    continue;

                if (expected.IsExpressionType())
                    lambda.MakeExpressionTree(ctx, expected);

                if (lambda.MustInferArgTypes)
                {
                    var actualWrapper = ReflectionHelper.WrapDelegate(ctx.Resolver, expected.Materialize());
                    lambda.SetInferredArgumentTypes(ctx, actualWrapper.ArgumentTypes);
                }

                lambda.Resolve(ctx);
            }
        }

        #endregion

        #region Debug

        protected bool Equals(InvocationNodeBase other)
        {
            return Arguments.SequenceEqual(other.Arguments);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((InvocationNodeBase) obj);
        }

        public override int GetHashCode()
        {
            return (Arguments != null ? Arguments.GetHashCode() : 0);
        }

        #endregion
    }
}
