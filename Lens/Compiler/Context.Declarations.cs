using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Resolver;
using Lens.SyntaxTree.Declarations;
using Lens.Translations;

namespace Lens.Compiler
{
    internal partial class Context
    {
        #region Fields

        /// <summary>
        /// The declaration blocks read so far, waiting for the whole script to have been read.
        ///
        /// Verification cannot happen as the block is read: a declaration may name a record that
        /// the script declares further down, and a function of the same name may be defined below
        /// it too.
        /// </summary>
        private readonly List<DeclarationBlockNode> _pendingDeclarations = new List<DeclarationBlockNode>();

        /// <summary>
        /// Whether anything that is not a 'use' or a 'declare' has been read yet.
        /// </summary>
        private bool _hasScriptContents;

        #endregion

        #region Reading

        /// <summary>
        /// Records a declaration block for later verification.
        /// </summary>
        private void DeclareEnvironment(DeclarationBlockNode node)
        {
            if (_hasScriptContents)
                Error(node, CompilerMessages.DeclareBlockNotAtTop);

            // references are the one entry that cannot wait: everything below the block - and the
            // rest of the block itself - may name a type that only the referenced assembly has
            foreach (var curr in node.Entries.OfType<DeclaredReference>())
                WithRecovery(() => ProcessReference(curr));

            _pendingDeclarations.Add(node);
        }

        /// <summary>
        /// Makes the types of a referenced assembly available to the script.
        ///
        /// This is the one place where a reference is more than documentation. A host that embeds
        /// LENS registers its own assemblies and needs no help, but a script that is edited on its
        /// own - in an editor, with no host anywhere - has nothing else to say what it may name.
        /// Failing to load is therefore a warning and not an error: the host may well have
        /// registered the assembly already, and the script is none the worse for saying so twice.
        /// </summary>
        private void ProcessReference(DeclaredReference entry)
        {
            // a sandboxed script does not get to decide which code is loaded into the host process:
            // merely loading an assembly runs its module initializers, whatever the type checks say
            if (Options.SafeMode != SafeMode.Disabled)
            {
                Error(entry, CompilerMessages.SafeModeIllegalReference, entry.Path);
                return;
            }

            if (AssemblyReferenceResolver.TryResolve(entry.Path, Options.ScriptDirectory, AssemblyCache.Assemblies, out var assembly, out var error))
            {
                RegisterAssembly(assembly);
                return;
            }

            Diagnostics.Add(DiagnosticSeverity.Warning, entry, string.Format(CompilerMessages.DeclaredReferenceNotFound, entry.Path, error));
        }

        #endregion

        #region Verification

        /// <summary>
        /// Reconciles the declared environment with the one the host actually registered.
        ///
        /// Declarations are an assertion, not a filter: a host may serve many scripts and register
        /// far more than any one of them uses, so anything registered but not declared is left
        /// alone. The checks run the other way round only - everything declared must exist, and
        /// must have the declared shape.
        ///
        /// The one entry that is not an assertion is the type alias, which the compiler can satisfy
        /// by itself from the referenced assemblies.
        /// </summary>
        private void ProcessDeclarations()
        {
            if (_pendingDeclarations.Count == 0)
                return;

            var entries = _pendingDeclarations.SelectMany(x => x.Entries).ToList();
            _pendingDeclarations.Clear();

            // aliases first: a variable or a function below may well be declared in terms of one
            var aliases = new HashSet<string>();
            foreach (var curr in entries.OfType<DeclaredTypeAlias>())
                WithRecovery(() => ProcessTypeAlias(curr, aliases));

            var properties = new HashSet<string>();
            foreach (var curr in entries.OfType<DeclaredProperty>())
                WithRecovery(() => ProcessProperty(curr, properties));

            var functions = new HashSet<string>();
            foreach (var curr in entries.OfType<DeclaredFunction>())
                WithRecovery(() => ProcessFunction(curr, functions));

            // DeclaredReference is not processed here: an assembly has to be in place before
            // anything that might name a type from it is read, so it is handled as the block is
            // read rather than once the whole script has been. See ProcessReference.
        }

        /// <summary>
        /// Registers a local name for a host type, or checks it against the one the host registered.
        /// </summary>
        private void ProcessTypeAlias(DeclaredTypeAlias entry, HashSet<string> seen)
        {
            if (!seen.Add(entry.Alias))
                Error(entry, CompilerMessages.DeclaredTypeDuplicated, entry.Alias);

            var target = ResolveType(entry.Type);

            if (!IsTypeAllowed(target))
                Error(entry, CompilerMessages.SafeModeIllegalType, target.FullName);

            var existing = FindType(entry.Alias);
            if (existing == null)
            {
                ImportType(entry.Alias, target.Materialize());
                return;
            }

            if (!existing.IsImported)
                Error(entry, CompilerMessages.DeclaredTypeConflict, entry.Alias);

            if (existing.TypeInfo != target)
                Error(entry, CompilerMessages.DeclaredTypeMismatch, entry.Alias, target.FullName, existing.TypeInfo.FullName);
        }

        /// <summary>
        /// Checks a declared variable against the property the host registered under that name.
        /// </summary>
        private void ProcessProperty(DeclaredProperty entry, HashSet<string> seen)
        {
            if (!seen.Add(entry.Name))
                Error(entry, CompilerMessages.DeclaredPropertyDuplicated, entry.Name);

            var declaredType = ResolveType(entry.Type);

            if (!_definedProperties.TryGetValue(entry.Name, out var actual))
            {
                if (Options.DeclarationMode == DeclarationMode.Provide)
                {
                    // no getter delegate and no property id: nothing here is ever invoked, this
                    // environment exists to be looked at
                    _definedProperties[entry.Name] = new GlobalPropertyInfo(-1, declaredType.Materialize(), true, entry.IsMutable, null, null);
                    return;
                }

                Error(entry, CompilerMessages.DeclaredPropertyMissing, entry.Name);
            }

            // exactly, not by assignability: a property declared wider than it is would still
            // compile, and would then make an editor offer the members of the wrong type
            var actualType = TypeEntryCache.Of(actual.PropertyType);
            if (actualType != declaredType)
                Error(entry, CompilerMessages.DeclaredPropertyTypeMismatch, entry.Name, declaredType.FullName, actualType.FullName);

            if (entry.IsMutable && !actual.HasSetter)
                Error(entry, CompilerMessages.DeclaredPropertyNotWritable, entry.Name);

            // 'let' narrows the property for the rest of the script, rather than merely asserting
            // something about it. An editor has the declaration and nothing else, so if a 'let'
            // over a writable property stayed writable here, every assignment to it would be an
            // error in the editor and fine in the compiler.
            if (!entry.IsMutable && actual.HasSetter)
            {
                _definedProperties[entry.Name] = new GlobalPropertyInfo(
                    actual.PropertyId,
                    actual.PropertyType,
                    actual.HasGetter,
                    false,
                    actual.GetterMethod,
                    null
                );
            }
        }

        /// <summary>
        /// Checks a declared function against the functions callable by that name.
        /// </summary>
        private void ProcessFunction(DeclaredFunction entry, HashSet<string> seen)
        {
            var argTypes = entry.Arguments.Select(x => x.GetArgumentType(this)).ToArray();
            var returnType = ResolveReturnType(entry.ReturnTypeSignature);

            var key = entry.Name + "(" + string.Join(",", argTypes.Select(x => x.FullName)) + ")";
            if (!seen.Add(key))
                Error(entry, CompilerMessages.DeclaredFunctionDuplicated, entry.Name);

            if (MatchesMethodGroup(entry.Name, argTypes, returnType))
                return;

            // RegisterFunction(name, someDelegate) does not create a method at all - it registers a
            // readonly property whose value is the delegate. Both shapes are callable as 'name a b',
            // so both satisfy a declaration.
            var hasProperty = _definedProperties.TryGetValue(entry.Name, out var prop);
            if (hasProperty && MatchesDelegate(prop.PropertyType, argTypes, returnType))
                return;

            if (Options.DeclarationMode == DeclarationMode.Provide)
            {
                MainType.DeclareMethod(entry.Name, returnType, entry.Arguments, entry.Arguments.Count > 0 && entry.Arguments[entry.Arguments.Count - 1].IsVariadic);
                return;
            }

            if (!hasProperty && !MainType.HasMethodGroup(entry.Name))
                Error(entry, CompilerMessages.DeclaredFunctionMissing, entry.Name);

            Error(
                entry,
                CompilerMessages.DeclaredFunctionSignatureMismatch,
                entry.Name,
                string.Join(" ", argTypes.Select(x => x.FullName)),
                returnType.FullName
            );
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Resolves a declared return type, defaulting to unit exactly as a function body does.
        /// </summary>
        private TypeEntry ResolveReturnType(TypeSignature signature)
        {
            return signature == null || string.IsNullOrEmpty(signature.FullSignature)
                ? TypeEntryCache.Of<UnitType>()
                : ResolveType(signature);
        }

        /// <summary>
        /// Checks whether any function callable by the given name has the declared signature.
        /// </summary>
        private bool MatchesMethodGroup(string name, TypeEntry[] argTypes, TypeEntry returnType)
        {
            if (!MainType.HasMethodGroup(name))
                return false;

            foreach (var curr in MainType.ResolveMethodGroup(name))
            {
                if (curr.GenericParameterCount > 0)
                    continue;

                if (!curr.GetArgumentTypes(this).SequenceEqual(argTypes))
                    continue;

                if (SameReturnType(ResolveReturnType(curr.ReturnType, curr.ReturnTypeSignature), returnType))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the return type of a method entity whose signature may not have been resolved yet.
        /// </summary>
        private TypeEntry ResolveReturnType(TypeEntry resolved, TypeSignature signature)
        {
            return resolved ?? ResolveReturnType(signature);
        }

        /// <summary>
        /// Checks whether a delegate-typed value is callable with the declared signature.
        /// </summary>
        private static bool MatchesDelegate(Type type, TypeEntry[] argTypes, TypeEntry returnType)
        {
            if (type == null || !typeof(Delegate).IsAssignableFrom(type))
                return false;

            var invoke = type.GetMethod("Invoke");
            if (invoke == null)
                return false;

            var actualArgs = invoke.GetParameters().Select(p => TypeEntryCache.Of(p.ParameterType)).ToArray();
            if (!actualArgs.SequenceEqual(argTypes))
                return false;

            return SameReturnType(TypeEntryCache.Of(invoke.ReturnType), returnType);
        }

        /// <summary>
        /// Compares two return types, treating System.Void and the compiler's unit type as one.
        /// </summary>
        private static bool SameReturnType(TypeEntry left, TypeEntry right)
        {
            return left.IsVoid() ? right.IsVoid() : left == right;
        }

        #endregion
    }
}
