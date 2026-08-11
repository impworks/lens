using System.Collections.Generic;
using System.Linq;
using Lens.Resolver;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.Utils;

namespace Lens.Compiler
{
    internal partial class Context
    {
        #region Fields

        /// <summary>
        /// The resolved type of every expression that has been bound so far.
        ///
        /// This used to be a field on the node itself, which meant a parse tree could only ever be
        /// bound once. Keeping it here instead is what lets the same tree be analysed repeatedly.
        /// </summary>
        private readonly Dictionary<NodeBase, TypeEntry> _expressionTypes = new Dictionary<NodeBase, TypeEntry>(ReferenceEqualityComparer<NodeBase>.Instance);

        /// <summary>
        /// The node each expanded node is to be compiled as.
        ///
        /// Binding used to overwrite the node in its parent, so that the tree no longer resembled
        /// the source once compilation was done. The expansion is recorded here instead, and
        /// emission consults it - which is what "emission walks the bound tree" means.
        /// </summary>
        private readonly Dictionary<NodeBase, NodeBase> _expansions = new Dictionary<NodeBase, NodeBase>(ReferenceEqualityComparer<NodeBase>.Instance);

        /// <summary>
        /// The nodes that must produce the address of their value rather than the value itself.
        ///
        /// This used to be a settable flag on the node, which made it part of the node's structural
        /// equality even though it says nothing about the source.
        /// </summary>
        private readonly HashSet<NodeBase> _pointerRequired = new HashSet<NodeBase>(ReferenceEqualityComparer<NodeBase>.Instance);

        /// <summary>
        /// The binding record of every node that has one: whatever a node used to memoize in its
        /// own fields while being resolved - the method an invocation picked, the closure method a
        /// lambda was compiled into, and so on.
        /// </summary>
        private readonly Dictionary<NodeBase, object> _nodeBindings = new Dictionary<NodeBase, object>(ReferenceEqualityComparer<NodeBase>.Instance);

        /// <summary>
        /// The scope frame of every code block that has been bound.
        ///
        /// A block used to own its frame, which meant the locals declared on one compilation were
        /// still declared on the next one.
        /// </summary>
        private readonly Dictionary<NodeBase, Scope> _scopes = new Dictionary<NodeBase, Scope>(ReferenceEqualityComparer<NodeBase>.Instance);

        #endregion

        #region Scopes

        /// <summary>
        /// Every local variable binding has declared, across every scope of the script.
        /// Each one carries where it was declared and every place that names it.
        /// </summary>
        public IEnumerable<Local> LocalSymbols => _scopes.Values.SelectMany(x => x.Locals.Values);

        /// <summary>
        /// Returns the scope frame of a code block, creating it on first request.
        /// </summary>
        public Scope ScopeOf(CodeBlockNode block)
        {
            if (_scopes.TryGetValue(block, out var existing))
                return existing;

            var created = new Scope(block.ScopeKind);
            _scopes[block] = created;
            return created;
        }

        #endregion

        #region Node binding records

        /// <summary>
        /// Returns the binding record of a node, creating an empty one on first request.
        /// </summary>
        public T BindingOf<T>(NodeBase node)
            where T : class, new()
        {
            if (_nodeBindings.TryGetValue(node, out var existing))
                return (T) existing;

            var created = new T();
            _nodeBindings[node] = created;
            return created;
        }

        #endregion

        #region Pointer requests

        /// <summary>
        /// Requests that a node emit the address of its value.
        /// </summary>
        public void RequirePointer(IPointerProvider provider)
        {
            if (provider is NodeBase node)
                _pointerRequired.Add(node);
        }

        /// <summary>
        /// Checks whether a node was asked for the address of its value.
        /// </summary>
        public bool IsPointerRequired(NodeBase node)
        {
            return _pointerRequired.Contains(node);
        }

        #endregion

        #region Expression types

        /// <summary>
        /// Returns the bound type of an expression, or null if it has not been bound yet.
        /// </summary>
        public TypeEntry FindExpressionType(NodeBase node)
        {
            return _expressionTypes.TryGetValue(node, out var type) ? type : null;
        }

        /// <summary>
        /// Records the bound type of an expression.
        /// </summary>
        public void SetExpressionType(NodeBase node, TypeEntry type)
        {
            _expressionTypes[node] = type;
        }

        /// <summary>
        /// Forgets the bound type of an expression, so that it is recomputed on the next request.
        /// Needed when the information a node was bound against arrives late: the argument types
        /// of a lambda are only known once the delegate it is being converted to is.
        /// </summary>
        public void ResetExpressionType(NodeBase node)
        {
            _expressionTypes.Remove(node);
        }

        #endregion

        #region Expansions

        /// <summary>
        /// Records that a node is to be compiled as another one.
        /// </summary>
        public void SetExpansion(NodeBase node, NodeBase expansion)
        {
            _expansions[node] = expansion;
        }

        /// <summary>
        /// Follows the expansion chain of a node and returns the node that is to be compiled in
        /// its place - the node itself, if it was not expanded.
        /// </summary>
        public NodeBase Expanded(NodeBase node)
        {
            if (node == null)
                return null;

            var curr = node;
            while (_expansions.TryGetValue(curr, out var next))
                curr = next;

            return curr;
        }

        #endregion
    }
}
