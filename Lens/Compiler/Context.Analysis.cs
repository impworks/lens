using System;
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
        /// The namespaces that sit directly under a prefix, as their last segment alone: an empty
        /// prefix answers with the roots ("System", "Microsoft"), and "System" with "Collections",
        /// "Linq" and the rest.
        /// </summary>
        internal IEnumerable<string> NamespacesUnder(string prefix)
        {
            var scope = string.IsNullOrEmpty(prefix) ? "" : prefix + ".";
            var result = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var curr in AssemblyCache.Namespaces)
            {
                if (curr.Length <= scope.Length || !curr.StartsWith(scope, StringComparison.Ordinal))
                    continue;

                // the deeper namespaces are in the list too, and each of them contributes the one
                // segment that follows the prefix rather than the whole of its tail
                var dot = curr.IndexOf('.', scope.Length);
                result.Add(dot < 0 ? curr.Substring(scope.Length) : curr.Substring(scope.Length, dot - scope.Length));
            }

            return result;
        }

        /// <summary>
        /// The host types a script can name without qualifying them: everything exported by a
        /// namespace that type resolution looks in.
        ///
        /// That is not the same as the namespaces the script imported. Three assemblies contribute
        /// namespaces of their own - System.Collections.Generic among them - and a name in one of
        /// those resolves with no 'use' directive at all, so a list built out of the imports alone
        /// would leave out most of what a script actually writes.
        /// </summary>
        internal IEnumerable<Type> TypesInScope()
        {
            var namespaces = new HashSet<string>(Namespaces.Keys, StringComparer.Ordinal);

            foreach (var asm in AssemblyCache.Assemblies)
            {
                var extras = TypeResolver.ImplicitNamespacesOf(asm);
                if (extras == null)
                    continue;

                foreach (var curr in extras)
                    namespaces.Add(curr);
            }

            return namespaces.SelectMany(TypesInNamespace);
        }

        /// <summary>
        /// The host types declared directly in a namespace, for a name that spells its own.
        /// </summary>
        internal IEnumerable<Type> TypesInNamespace(string nsp)
        {
            return AssemblyCache.TypesIn(nsp).Where(IsTypeAllowed);
        }

        /// <summary>
        /// Enumerates the extension methods applicable to a type, grouped by name.
        /// </summary>
        internal Dictionary<string, List<MethodInfo>> ExtensionMethodsOf(TypeEntry type)
        {
            if (!Options.AllowExtensionMethods)
                return new Dictionary<string, List<MethodInfo>>();

            var found = _extensionResolver.EnumerateExtensionMethods(Resolver, type);
            if (_safeModeRules.Mode == SafeMode.Disabled)
                return found;

            // the editor must offer what the compiler accepts: safe mode refuses an extension method
            // whose declaring type is out of bounds, so a completion list that showed one would be
            // offering a call that cannot be made
            var allowed = new Dictionary<string, List<MethodInfo>>();
            foreach (var pair in found)
            {
                var methods = pair.Value.Where(x => IsTypeAllowed(x.DeclaringType) && IsMemberAllowed(new MethodWrapper(x))).ToList();
                if (methods.Count > 0)
                    allowed[pair.Key] = methods;
            }

            return allowed;
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
