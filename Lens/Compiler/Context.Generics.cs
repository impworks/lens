using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.SyntaxTree.Declarations;
using Lens.Translations;

namespace Lens.Compiler
{
    internal partial class Context
    {
        #region Type parameter declaration

        /// <summary>
        /// Types that the CLI forbids as a base type constraint.
        /// </summary>
        private static readonly Type[] ForbiddenConstraintTypes =
        {
            typeof(object),
            typeof(Array),
            typeof(ValueType),
            typeof(Enum),
            typeof(Delegate),
            typeof(MulticastDelegate)
        };

        /// <summary>
        /// Creates the compiler's model for a list of declared type parameters.
        /// Only the keyword constraints are validated here: the type constraints may name a
        /// sibling parameter and can therefore only be resolved once all builders exist.
        /// </summary>
        public List<GenericParameterEntity> CreateGenericParameters(IEnumerable<TypeParameterDefinition> definitions, string declarationName)
        {
            if (definitions == null)
                return null;

            var result = new List<GenericParameterEntity>();

            foreach (var curr in definitions)
            {
                if (result.Any(x => x.Name == curr.Name))
                    Error(curr, CompilerMessages.TypeParameterRedefinition, curr.Name, declarationName);

                var entity = new GenericParameterEntity(curr.Name, result.Count, declarationName)
                {
                    IsReferenceType = curr.IsReferenceType,
                    IsValueType = curr.IsValueType,
                    RequiresDefaultCtor = curr.RequiresDefaultCtor
                };

                entity.TypeConstraintSignatures.AddRange(curr.TypeConstraints);

                ValidateKeywordConstraints(curr, entity);

                result.Add(entity);
            }

            return result.Count > 0 ? result : null;
        }

        /// <summary>
        /// Checks the "class", "struct" and "new" constraints of a single parameter against each other.
        /// </summary>
        private void ValidateKeywordConstraints(TypeParameterDefinition definition, GenericParameterEntity entity)
        {
            foreach (var group in definition.Keywords.GroupBy(x => x))
                if (group.Count() > 1)
                    Error(definition, CompilerMessages.GenericConstraintDuplicateKeyword, group.Key, entity.Name, entity.DeclarationName);

            if (entity.IsReferenceType && entity.IsValueType)
                Error(definition, CompilerMessages.GenericConstraintClassAndStruct, entity.Name, entity.DeclarationName);

            if (entity.IsValueType && entity.RequiresDefaultCtor)
                Error(definition, CompilerMessages.GenericConstraintStructAndNew, entity.Name, entity.DeclarationName);
        }

        #endregion

        #region Type parameter resolution

        /// <summary>
        /// Resolves the type constraints of a declaration's parameters and applies them to the builders.
        /// Must be called after all the builders of the declaration have been created, because a
        /// constraint may name a sibling parameter.
        /// </summary>
        public void ResolveGenericParameters(List<GenericParameterEntity> parameters)
        {
            foreach (var curr in parameters)
                Resolver.Register(curr);

            if (parameters.Count > 0 && parameters[0].Source != null)
            {
                ApplyForwardedConstraints(parameters);
            }
            else
            {
                WithGenericScope(parameters, () =>
                    {
                        foreach (var curr in parameters)
                            ResolveConstraintsOf(curr);
                    }
                );
            }

            foreach (var curr in parameters)
                CheckCircularConstraints(curr);

            foreach (var curr in parameters)
                ApplyConstraints(curr);
        }

        /// <summary>
        /// Separates a parameter's type constraints into a base type and a set of interfaces.
        /// </summary>
        private void ResolveConstraintsOf(GenericParameterEntity entity)
        {
            foreach (var signature in entity.TypeConstraintSignatures)
            {
                var type = ResolveType(signature);

                if (type.IsInterface)
                {
                    if (entity.Interfaces.Contains(type))
                        Error(signature, CompilerMessages.GenericConstraintDuplicateInterface, type, entity.Name, entity.DeclarationName);

                    entity.Interfaces.Add(type);
                    continue;
                }

                if (entity.BaseType != null)
                    Error(signature, CompilerMessages.GenericConstraintMultipleBaseTypes, entity.Name, entity.DeclarationName, entity.BaseType, type);

                if (entity.IsReferenceType)
                    Error(signature, CompilerMessages.GenericConstraintBaseTypeAndKeyword, entity.Name, entity.DeclarationName, type, "class");

                if (entity.IsValueType)
                    Error(signature, CompilerMessages.GenericConstraintBaseTypeAndKeyword, entity.Name, entity.DeclarationName, type, "struct");

                // a naked type parameter is a legal base constraint and carries none of the
                // restrictions below, since it is not a concrete type yet
                if (!type.IsGenericParameter && !IsLegalBaseConstraint(type))
                    Error(signature, CompilerMessages.GenericConstraintInvalidBaseType, type, entity.Name, entity.DeclarationName);

                entity.BaseType = type;
            }
        }

        /// <summary>
        /// Checks whether a concrete type may be used as a base type constraint.
        /// </summary>
        private static bool IsLegalBaseConstraint(TypeEntry type)
        {
            if (ForbiddenConstraintTypes.Contains(type.Materialize()))
                return false;

            // a static class is abstract and sealed at once
            return !type.IsSealed && !type.IsPointer && !type.IsByRef;
        }

        /// <summary>
        /// Detects a cycle in the chain of naked type parameter constraints, like 'T = K, K = T'.
        /// </summary>
        private void CheckCircularConstraints(GenericParameterEntity entity)
        {
            var visited = new HashSet<GenericParameterEntity> {entity};
            var curr = entity;

            while (true)
            {
                var next = Resolver.FindConstraints(curr.BaseType);
                if (next == null)
                    return;

                if (!visited.Add(next))
                    Error(CompilerMessages.GenericConstraintCircular, entity.Name, entity.DeclarationName);

                curr = next;
            }
        }

        /// <summary>
        /// Applies the resolved constraints to the generic parameter builder.
        /// </summary>
        private static void ApplyConstraints(GenericParameterEntity entity)
        {
            var attributes = GenericParameterAttributes.None;

            if (entity.IsReferenceType)
                attributes |= GenericParameterAttributes.ReferenceTypeConstraint;

            if (entity.IsValueType)
                attributes |= GenericParameterAttributes.NotNullableValueTypeConstraint;

            if (entity.RequiresDefaultCtor)
                attributes |= GenericParameterAttributes.DefaultConstructorConstraint;

            if (attributes != GenericParameterAttributes.None)
                entity.Builder.SetGenericParameterAttributes(attributes);

            // the CLI spells a value type constraint as ValueType plus the attribute
            var baseType = entity.BaseType ?? (entity.IsValueType ? TypeEntryCache.Of<ValueType>() : null);
            if (baseType != null)
                entity.Builder.SetBaseTypeConstraint(baseType.Materialize());

            if (entity.Interfaces.Count > 0)
                entity.Builder.SetInterfaceConstraints(TypeEntry.Materialize(entity.Interfaces));
        }

        /// <summary>
        /// Creates a copy of an existing set of generic parameters for a compiler-generated type
        /// that has to forward the parameters of its enclosing declaration - a closure class or
        /// the cache holder of a pure function.
        ///
        /// The copies carry the original constraints, because the CLR checks that the arguments of
        /// a constructed type satisfy them.
        /// </summary>
        public List<GenericParameterEntity> CloneGenericParameters(IEnumerable<GenericParameterEntity> source, string declarationName)
        {
            var originals = source?.ToArray();
            if (originals == null || originals.Length == 0)
                return null;

            var result = new List<GenericParameterEntity>();
            foreach (var curr in originals)
            {
                result.Add(
                    new GenericParameterEntity(curr.Name, result.Count, declarationName)
                    {
                        IsReferenceType = curr.IsReferenceType,
                        IsValueType = curr.IsValueType,
                        RequiresDefaultCtor = curr.RequiresDefaultCtor,
                        Source = curr
                    }
                );
            }

            return result;
        }

        /// <summary>
        /// Copies the type constraints of a forwarded parameter, rewriting any reference to the
        /// original parameters into a reference to their copies. A constraint that mentioned the
        /// enclosing declaration's parameters would otherwise be illegal metadata.
        /// </summary>
        private static void ApplyForwardedConstraints(List<GenericParameterEntity> parameters)
        {
            var sources = parameters.Select(x => (Type) x.Source.Builder).ToArray();
            var targets = parameters.Select(x => (Type) x.Builder).ToArray();

            foreach (var curr in parameters)
            {
                if (curr.Source.BaseType != null)
                    curr.BaseType = TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(curr.Source.BaseType.Materialize(), sources, targets, false));

                foreach (var iface in curr.Source.Interfaces)
                    curr.Interfaces.Add(TypeEntryCache.Of(GenericHelper.ApplyGenericArguments(iface.Materialize(), sources, targets, false)));
            }
        }

        /// <summary>
        /// The generic parameters that are visible at the point where a compiler-generated type
        /// is created: those of the enclosing type and those of the enclosing method.
        /// </summary>
        public List<GenericParameterEntity> EnclosingGenericParameters()
        {
            var result = new List<GenericParameterEntity>();

            if (CurrentType?.GenericParameters != null)
                result.AddRange(CurrentType.GenericParameters);

            var method = CurrentMethod as MethodEntity;
            if (method?.GenericParameters != null)
                result.AddRange(method.GenericParameters);

            return result;
        }

        #endregion

        #region Generic scope

        /// <summary>
        /// Runs an action with the given generic parameters in scope, so that a type signature
        /// that names one of them resolves to the parameter and not to a host type.
        /// </summary>
        public void WithGenericScope(IEnumerable<GenericParameterEntity> parameters, Action action)
        {
            var list = parameters?.ToArray();
            if (list == null || list.Length == 0)
            {
                action();
                return;
            }

            Resolver.EnterGenericScope(list);
            try
            {
                action();
            }
            finally
            {
                Resolver.ExitGenericScope();
            }
        }

        #endregion
    }
}
