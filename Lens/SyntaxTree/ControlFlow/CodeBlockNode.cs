using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.SyntaxTree.Declarations.Locals;
using Lens.SyntaxTree.Internals;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
    /// <summary>
    /// A set of consecutive code statements.
    /// </summary>
    internal class CodeBlockNode : NodeBase, IEnumerable<NodeBase>
    {
        #region Constructor

        public CodeBlockNode(ScopeKind scopeKind = ScopeKind.Unclosured)
        {
            Statements = new List<NodeBase>();
            ScopeKind = scopeKind;
        }

        #endregion

        #region Fields

        /// <summary>
        /// What kind of frame this block opens. The frame itself is a binding result and belongs to
        /// the context - see Context.ScopeOf.
        /// </summary>
        public ScopeKind ScopeKind { get; }

        /// <summary>
        /// The statements to execute.
        /// </summary>
        public List<NodeBase> Statements { get; set; }

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            var last = Statements.LastOrDefault(x => !(x is IMetaNode));
            if (last is VarNode || last is LetNode)
                Error(last, CompilerMessages.CodeBlockLastVar);

            ctx.EnterScope(ctx.ScopeOf(this));

            var result = TypeEntryCache.Of<UnitType>();
            foreach (var curr in Statements)
            {
                if (!(curr is IMetaNode))
                    result = curr.Resolve(ctx);
            }

            ctx.ExitScope();

            return result;
        }

        #endregion

        #region Transform

        public override void Transform(Context ctx, bool mustReturn)
        {
            ctx.EnterScope(ctx.ScopeOf(this));

            // a statement is the unit of error recovery: a broken statement must not hide the
            // problems in the ones that follow it
            foreach (var child in GetChildren().ToArray())
                ctx.WithRecovery(() => TransformChild(ctx, child, mustReturn));

            ctx.ExitScope();
        }

        internal override IEnumerable<NodeChild> GetChildren()
        {
            return Statements.Select((stmt, i) => new NodeChild(stmt));
        }

        #endregion

        #region Closures

        public override void AnalyzeClosures(Context ctx)
        {
            ctx.EnterScope(ctx.ScopeOf(this));
            base.AnalyzeClosures(ctx);
            ctx.ExitScope().AnalyzeSelf(ctx);
        }

        public override void EmitClosureEntities(Context ctx)
        {
            ctx.EnterScope(ctx.ScopeOf(this));
            base.EmitClosureEntities(ctx);
            ctx.ExitScope().EmitSelf(ctx);
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var scope = ctx.ScopeOf(this);
            ctx.EnterScope(scope);

            // a machine's frame needs no setting up: the closure instance is the receiver, and it
            // was created by the function that handed the machine out
            if (scope.ClosureType != null && !scope.ClosureIsThis)
                EmitClosureSetup(ctx, scope);

            EmitStatements(ctx, mustReturn);

            ctx.ExitScope();
        }

        /// <summary>
        /// Emits code that initializes the scope variable for closures and lambdas to work.
        /// </summary>
        private void EmitClosureSetup(Context ctx, Scope scope)
        {
            var gen = ctx.CurrentMethod.Generator;

            var type = scope.ClosureInstanceType;
            var loc = scope.ClosureVariable;

            // create closure instance
            var closureCtor = ctx.ResolveConstructor(type, new TypeEntry[0]);
            gen.EmitCreateObject(closureCtor.ConstructorInfo);
            gen.EmitSaveLocal(loc);

            // affix to parent
            if (scope.ClosureParent != null)
            {
                gen.EmitLoadLocal(loc);

                // a state machine's frame is the receiver, so a closure nested inside one affixes
                // itself to 'this' just as a closure in another method does
                if (scope.ClosureParentIsRemote || scope.ClosureParent.ClosureIsThis)
                    gen.EmitLoadArgument(0);
                else
                    gen.EmitLoadLocal(scope.ClosureParent.ClosureVariable);

                gen.EmitSaveField(ctx.ResolveField(type, EntityNames.ParentScopeFieldName).FieldInfo);
            }

            // save arguments into closure
            foreach (var curr in scope.Locals.Values)
            {
                if (!curr.IsClosured || curr.ArgumentId == null)
                    continue;

                gen.EmitLoadLocal(loc);
                gen.EmitLoadArgument(curr.ArgumentId.Value);
                gen.EmitSaveField(ctx.ResolveField(type, curr.ClosureFieldName).FieldInfo);
            }
        }

        /// <summary>
        /// Emits the list of statements one by one.
        /// </summary>
        private void EmitStatements(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            var lastExpressionIdx = Statements.FindLastIndex(x => !(x is IMetaNode));

            for (var idx = 0; idx < Statements.Count; idx++)
            {
                var subReturn = mustReturn && (idx == lastExpressionIdx || ScopeKind == ScopeKind.MatchRoot);

                // the statement that gets compiled is the one binding expanded this one into
                var curr = ctx.Expanded(Statements[idx]);

                var retType = curr.Resolve(ctx, subReturn);

                if (!subReturn && curr.IsConstant)
                    continue;

                // the position comes from the statement as it was written, not from whatever it was
                // expanded into: an expansion is synthesized and has no place in the source
                ctx.DebugInfo?.MarkStatement(gen, Statements[idx]);

                curr.Emit(ctx, subReturn);

                if (!subReturn && !retType.IsVoid())
                {
                    // nested code block nodes take care of themselves
                    if (!(curr is CodeBlockNode))
                        gen.EmitPop();
                }
            }
        }

        #endregion

        #region Debug

        protected bool Equals(CodeBlockNode other)
        {
            return Statements.SequenceEqual(other.Statements);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((CodeBlockNode) obj);
        }

        public override int GetHashCode()
        {
            return (Statements != null ? Statements.GetHashCode() : 0);
        }

        #endregion

        #region IEnumerable<NodeBase> implementation

        public IEnumerator<NodeBase> GetEnumerator()
        {
            return Statements.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(NodeBase node)
        {
            Statements.Add(node);
        }

        public void AddRange(params NodeBase[] nodes)
        {
            Statements.AddRange(nodes);
        }

        public void AddRange(IEnumerable<NodeBase> nodes)
        {
            Statements.AddRange(nodes);
        }

        public void Insert(NodeBase node)
        {
            Statements.Insert(0, node);
        }

        #endregion

        #region Additional methods

        /// <summary>
        /// Loads nodes from other block.
        /// </summary>
        public void LoadFrom(CodeBlockNode other)
        {
            Statements = other.Statements;
        }

        #endregion
    }
}