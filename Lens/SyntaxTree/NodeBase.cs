using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree
{
    /// <summary>
    /// The base class for all syntax tree nodes.
    /// </summary>
    internal abstract class NodeBase : LocationEntity
    {
        #region Resolve

        /// <summary>
        /// Returns or resolves the type of expression represented by current node.
        /// The result is memoized in the context rather than in the node, so that the same tree
        /// can be bound more than once.
        /// </summary>
        [DebuggerStepThrough]
        public TypeEntry Resolve(Context ctx, bool mustReturn = true)
        {
            var cached = ctx.FindExpressionType(this);
            if (cached != null)
                return cached;

            try
            {
                var type = ResolveInternal(ctx, mustReturn);
                CheckTypeInSafeMode(ctx, type);
                ctx.SetExpressionType(this, type);
                return type;
            }
            catch (LensCompilerException ex)
            {
                if (ex.EndLocation == null || ex.StartLocation == null)
                    ex.BindToLocation(this);

                throw;
            }
        }

        /// <summary>
        /// Resolves the expression type.
        /// Must be overridden in child types if they represent a meaninful value.
        /// </summary>
        protected virtual TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            return TypeEntryCache.Of<UnitType>();
        }

        #endregion

        #region Transform

        /// <summary>
        /// Enables recursive children resolution & expansion.
        /// </summary>
        public virtual void Transform(Context ctx, bool mustReturn)
        {
            var children = GetChildren().ToArray();
            foreach (var child in children)
                TransformChild(ctx, child, child?.MustReturn ?? mustReturn);
        }

        /// <summary>
        /// Resolves a single child node, expands it if it wants to be, and recurses into it.
        /// </summary>
        protected static void TransformChild(Context ctx, NodeChild child, bool mustReturn)
        {
            if (child?.Node == null)
                return;

            child.Node.Resolve(ctx, mustReturn);
            var sub = child.Node.Expand(ctx, mustReturn);
            if (sub != null)
            {
                // the parse tree is left alone: the expansion is recorded on the side and
                // emission picks it up from there
                ctx.SetExpansion(child.Node, sub);

                try
                {
                    sub.Resolve(ctx, mustReturn);
                    sub.Transform(ctx, mustReturn);
                }
                catch (LensCompilerException ex)
                {
                    // an expansion is synthesized and the nodes it is made of have no location of
                    // their own, so an error raised inside one would otherwise reach the top with no
                    // position at all and be reported against whatever the script starts with. It
                    // belongs to the source the expansion came from.
                    if (ex.StartLocation == null || ex.EndLocation == null)
                        ex.BindToLocation(child.Node);

                    throw;
                }
            }
            else
            {
                child.Node.Transform(ctx, mustReturn);
            }
        }

        /// <summary>
        /// Checks if current node can be expanded into another node or a set of nodes.
        /// To be overridden in child nodes if required.
        /// </summary>
        /// <returns>
        /// Null if no expansion is suitable, a NodeBase object instance otherwise.
        /// </returns>
        protected virtual NodeBase Expand(Context ctx, bool mustReturn)
        {
            return null;
        }

        /// <summary>
        /// Gets the list of child nodes.
        /// </summary>
        internal virtual IEnumerable<NodeChild> GetChildren()
        {
            yield break;
        }

        #endregion

        #region Rewriting

        /// <summary>
        /// The subexpressions this node evaluates, in the order it evaluates them - and only the
        /// ones it evaluates unconditionally.
        ///
        /// This is what lets an await be lifted out of the middle of an expression. Everything the
        /// expression would have evaluated before reaching the await has to be evaluated before it
        /// still, which means the rewrite has to know the order; and a node that evaluates a
        /// subexpression only sometimes - a short-circuiting operator, a match - cannot have its
        /// operands hoisted at all, because hoisting one would evaluate it in a case where the
        /// source says it should not be. Such a node reports no operands, and the rewrite deals
        /// with it by name or rejects it, rather than reordering something it does not understand.
        ///
        /// This is deliberately not GetChildren: that enumerates a node's subtree for binding, and
        /// is free to reach through a node into the one below it, which is exactly what a rewrite
        /// must not do.
        /// </summary>
        internal virtual IReadOnlyList<NodeBase> Operands => NoOperands;

        protected static readonly IReadOnlyList<NodeBase> NoOperands = new NodeBase[0];

        /// <summary>
        /// Builds a copy of this node with its operands replaced, positionally.
        /// The list is the one Operands returned, with some of its entries swapped out.
        /// </summary>
        internal virtual NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            return this;
        }

        /// <summary>
        /// Whether an operand may be evaluated into a temporary ahead of time.
        /// True for a value; false where the node needs the subexpression itself rather than what
        /// it evaluates to, and evaluating it early would hand the node a copy.
        /// </summary>
        internal virtual bool CanHoistOperand(int index)
        {
            return true;
        }

        /// <summary>
        /// A shallow copy, for a rewrite that replaces some of a node's operands and keeps
        /// everything else about it.
        ///
        /// Only ever taken before binding, while everything a node learns about itself still lives
        /// in a side table on the context and every field it caches is unset - so the copy and the
        /// original share nothing that either of them will later write to. Anything a constructor
        /// allocated, a list of arguments among them, is shared and has to be replaced by hand.
        /// </summary>
        internal T Copy<T>()
            where T : NodeBase
        {
            return (T) MemberwiseClone();
        }

        #endregion

        #region Closures

        /// <summary>
        /// Works out which locals are captured, by which lambda, and which scopes will therefore
        /// have to own a closure.
        ///
        /// This is analysis: it reads the bound model and writes only to the scope tree. No IL is
        /// generated and no assembly entity is created, which is what lets Phase 4 merge the
        /// hoisting a state machine does with the hoisting a closure does.
        /// </summary>
        public virtual void AnalyzeClosures(Context ctx)
        {
            // only the expansion is ever emitted, so only the expansion's captures matter
            foreach (var child in GetChildren())
                ctx.Expanded(child?.Node)?.AnalyzeClosures(ctx);
        }

        /// <summary>
        /// Creates the assembly entities that the closure analysis called for: the closure classes,
        /// their fields, the backing methods of lambdas, and the IL locals.
        /// </summary>
        public virtual void EmitClosureEntities(Context ctx)
        {
            foreach (var child in GetChildren())
                ctx.Expanded(child?.Node)?.EmitClosureEntities(ctx);
        }

        #endregion

        #region Emit

        /// <summary>
        /// Generates the IL for this node.
        /// </summary>
        /// <param name="ctx">Pointer to current context.</param>
        /// <param name="mustReturn">Flag indicating the node should return a value.</param>
        public void Emit(Context ctx, bool mustReturn)
        {
            // a node that binding expanded is compiled as its expansion
            var target = ctx.Expanded(this);

            if (target.IsConstant && !mustReturn)
                return;

            target.EmitInternal(ctx, mustReturn);
        }

        /// <summary>
        /// Emits the IL opcodes that represents the current node.
        /// </summary>
        protected virtual void EmitInternal(Context ctx, bool mustReturn)
        {
            throw new InvalidOperationException(
                $"Node '{GetType()}' neither has a body nor was expanded!"
            );
        }

        #endregion

        #region Constant checkers

        /// <summary>
        /// Checks if the current node is a constant.
        /// </summary>
        public virtual bool IsConstant => false;

        /// <summary>
        /// Returns a constant value corresponding to the current node.
        /// </summary>
        public virtual dynamic ConstantValue => throw new InvalidOperationException("Not a constant!");

        #endregion

        #region Helpers

        /// <summary>
        /// Reports an error to the compiler.
        /// </summary>
        /// <param name="message">Error message.</param>
        /// <param name="args">Optional error arguments.</param>
        [ContractAnnotation("=> halt")]
        [DebuggerStepThrough]
        protected void Error(string message, params object[] args)
        {
            Error(this, message, args);
        }

        /// <summary>
        /// Reports an error to the compiler.
        /// </summary>
        /// <param name="entity">Location entity to which the error is bound.</param>
        /// <param name="message">Error message.</param>
        /// <param name="args">Optional error arguments.</param>
        [ContractAnnotation("=> halt")]
        [DebuggerStepThrough]
        protected void Error(LocationEntity entity, string message, params object[] args)
        {
            var msg = string.Format(message, args);
            throw new LensCompilerException(msg, entity);
        }

        /// <summary>
        /// Aborts binding the current statement without reporting anything, because whatever went
        /// wrong here is a consequence of a mistake that has already been reported.
        /// </summary>
        [ContractAnnotation("=> halt")]
        [DebuggerStepThrough]
        protected void AbortQuietly(string message, params object[] args)
        {
            var msg = string.Format(message, args);
            throw new LensCompilerException(msg, this) {IsSuppressed = true};
        }

        /// <summary>
        /// Throws an error that the current type is not alowed in safe mode.
        /// </summary>
        protected void CheckTypeInSafeMode(Context ctx, TypeEntry type)
        {
            if (!ctx.IsTypeAllowed(type))
                Error(CompilerMessages.SafeModeIllegalType, type.FullName);
        }

        /// <summary>
        /// Throws an error that the member the node bound to is not allowed in safe mode.
        ///
        /// Not every restriction can be expressed as one about types. Type.GetType is handed the
        /// name of a type as a string, so whichever type comes back out of it was never named in
        /// the script and no type rule can have an opinion about it; the call is the only place the
        /// question can be asked.
        /// </summary>
        protected void CheckMemberInSafeMode(Context ctx, WrapperBase member)
        {
            if (!ctx.IsMemberAllowed(member))
                Error(CompilerMessages.SafeModeIllegalMember, member.Name, member.DeclaringType);
        }

        /// <summary>
        /// Re-infers the lambda if argument types were not specified before.
        /// </summary>
        protected static void EnsureLambdaInferred(Context ctx, NodeBase canBeLambda, TypeEntry delegateType)
        {
            var lambda = canBeLambda as LambdaNode;
            if (lambda == null)
                return;

            // a target that wants an expression tree describes the same signature, one level down,
            // and takes the lambda down the tree-building path
            if (delegateType.IsExpressionType())
                lambda.MakeExpressionTree(ctx, delegateType);
            else if (!delegateType.IsCallableType())
                return;

            var wrapper = ctx.WrapDelegate(delegateType);
            if (!wrapper.ReturnType.IsGenericParameter)
                lambda.SetInferredReturnType(ctx, wrapper.ReturnType);

            lambda.Resolve(ctx);

            if (lambda.MustInferArgTypes)
                lambda.SetInferredArgumentTypes(ctx, wrapper.ArgumentTypes);

            // an expression tree is not a delegate the literal becomes, it is a tree built out of
            // its body, and MakeExpressionTree above has already said so
            if (delegateType.IsExpressionType())
                return;

            // the literal becomes the delegate the context named, so long as it can: a signature
            // that does not fit settles into its own shape instead, and the conversion the context
            // implies is then what reports the mismatch - which is the message that says what is
            // actually wrong with it
            if (delegateType.IsExtendablyAssignableFrom(ctx.Resolver, lambda.Resolve(ctx)))
                lambda.SetTargetType(ctx, delegateType);
            else
                lambda.CommitToDefaultDelegate(ctx);
        }

        #endregion
    }
}