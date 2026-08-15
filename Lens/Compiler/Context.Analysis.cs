using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;

namespace Lens.Compiler
{
    /// <summary>
    /// What a bound script can be asked about, on top of what compiling it needs.
    ///
    /// None of this is used while compiling. It exists so that the analysis layer - and through it
    /// an editor - can ask where a name came from and what is visible at a point in the file,
    /// without reaching into the compiler's private state.
    /// </summary>
    internal partial class Context
    {
        #region Fields

        /// <summary>
        /// Every type signature that resolved, and what it resolved to.
        ///
        /// This is how a type gets a reference list. Rename needs one, and there is no other honest
        /// way to get it: a type is named by a signature rather than by an identifier node, so
        /// walking the tree would not find them, and matching text would find too much.
        /// </summary>
        private List<KeyValuePair<TypeSignature, TypeEntry>> _typeReferences;

        #endregion

        #region Properties

        /// <summary>
        /// Whether every resolved type signature is to be recorded. Off during compilation: this
        /// costs a list entry per resolution and nothing that emits IL has a use for it.
        /// </summary>
        internal bool TrackTypeReferences { get; set; }

        /// <summary>
        /// Every type signature the source wrote, paired with the type it named.
        /// </summary>
        internal IEnumerable<KeyValuePair<TypeSignature, TypeEntry>> TypeReferences =>
            _typeReferences ?? Enumerable.Empty<KeyValuePair<TypeSignature, TypeEntry>>();

        /// <summary>
        /// The names of every type visible to the script: declared, imported and aliased alike.
        /// </summary>
        internal IEnumerable<KeyValuePair<string, TypeEntity>> DefinedTypes => _definedTypes;

        /// <summary>
        /// The global variables the host registered, or that a declaration provided.
        /// </summary>
        internal IEnumerable<KeyValuePair<string, GlobalPropertyInfo>> DefinedProperties => _definedProperties;

        /// <summary>
        /// Every code block that was bound, with the scope frame it was bound in.
        /// </summary>
        internal IEnumerable<KeyValuePair<NodeBase, Scope>> BoundScopes => _scopes;

        #endregion

        #region Methods

        /// <summary>
        /// Records that a signature named a type.
        /// </summary>
        private void RecordTypeReference(TypeSignature signature, TypeEntry type)
        {
            // the compiler builds signatures of its own while expanding, and those name nothing
            // anybody could navigate to
            if (signature == null || ReferenceEquals(type, null))
                return;

            if (signature.StartLocation.Line == 0 && signature.StartLocation.Offset == 0)
                return;

            _typeReferences = _typeReferences ?? new List<KeyValuePair<TypeSignature, TypeEntry>>();
            _typeReferences.Add(new KeyValuePair<TypeSignature, TypeEntry>(signature, type));
        }

        /// <summary>
        /// Returns the innermost scope frame whose block contains the given position, and every
        /// frame enclosing it - which is exactly the set of names visible at that point.
        /// </summary>
        internal IEnumerable<Scope> ScopesAt(LexemLocation position)
        {
            var best = default(KeyValuePair<NodeBase, Scope>);
            var bestSize = int.MaxValue;

            foreach (var curr in _scopes)
            {
                var block = curr.Key;
                if (!Contains(block, position))
                    continue;

                var size = Size(block);
                if (size >= bestSize)
                    continue;

                best = curr;
                bestSize = size;
            }

            if (best.Value == null)
            {
                // outside every block: the script body is still in scope
                var root = _scopes.FirstOrDefault(x => x.Key == MainMethod.Body);
                if (root.Value != null)
                    yield return root.Value;

                yield break;
            }

            for (var scope = best.Value; scope != null; scope = scope.OuterScope)
                yield return scope;
        }

        /// <summary>
        /// The type the script declared that an entry stands for, whether the entry is the
        /// declaration itself or an instantiation of it: Foo and Foo&lt;int&gt; are both answered
        /// by the declaration of Foo, which is the only thing that has members to list.
        /// </summary>
        internal TypeEntity DeclarationOf(TypeEntry type)
        {
            return FindDeclaredType(type)?.Entity;
        }

        /// <summary>
        /// Rewrites the declared type of a member in the terms of the reference it is reached
        /// through: the X of Foo&lt;int&gt; is an int, whatever the declaration calls it.
        /// </summary>
        internal TypeEntry MemberTypeOf(TypeEntry type, TypeEntry memberType)
        {
            if (ReferenceEquals(memberType, null))
                return null;

            return FindDeclaredType(type)?.Substitute(memberType) ?? memberType;
        }

        /// <summary>
        /// Enumerates the extension methods applicable to a type, grouped by name.
        /// </summary>
        internal Dictionary<string, List<MethodInfo>> ExtensionMethodsOf(TypeEntry type)
        {
            if (!Options.AllowExtensionMethods || type.IsDeclared || type.ContainsDeclared)
                return new Dictionary<string, List<MethodInfo>>();

            return _extensionResolver.EnumerateExtensionMethods(Resolver, type.Materialize());
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Checks whether an entity spans the given position.
        /// </summary>
        internal static bool Contains(LocationEntity entity, LexemLocation position)
        {
            if (entity == null)
                return false;

            var start = entity.StartLocation;
            var end = entity.EndLocation;

            if (start.Line == 0 || end.Line == 0)
                return false;

            if (position.Line < start.Line || position.Line > end.Line)
                return false;

            if (position.Line == start.Line && position.Offset < start.Offset)
                return false;

            if (position.Line == end.Line && position.Offset > end.Offset)
                return false;

            return true;
        }

        /// <summary>
        /// A rough measure of how much source an entity covers, for picking the innermost of
        /// several that all contain a position.
        /// </summary>
        private static int Size(LocationEntity entity)
        {
            var lines = entity.EndLocation.Line - entity.StartLocation.Line;
            return lines * 1000 + (lines == 0 ? entity.EndLocation.Offset - entity.StartLocation.Offset : 0);
        }

        #endregion
    }
}
