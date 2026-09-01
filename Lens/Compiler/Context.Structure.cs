using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.Translations;

namespace Lens.Compiler
{
    internal partial class Context
    {
        #region Methods

        /// <summary>
        /// Creates a new type entity with given name.
        /// </summary>
        internal TypeEntity CreateType(string name, TypeSignature parent = null, bool isSealed = false, bool defaultCtor = true, bool prepare = true, List<GenericParameterEntity> genericParameters = null)
        {
            return CreateTypeCore(name, isSealed, defaultCtor, prepare, x =>
                {
                    x.ParentSignature = parent;
                    x.GenericParameters = genericParameters;
                }
            );
        }

        /// <summary>
        /// Settles a lambda literal that is being used as a value in its own right, and returns the
        /// type of the expression either way.
        ///
        /// A lambda literal has no delegate type of its own until something says which delegate it
        /// is to become - the parameter it is passed to, the location it is assigned to, the type it
        /// is cast to. Where nothing says, it becomes the Func or Action its own signature
        /// describes, and that is what this decides. Every caller is a context that uses the value
        /// as it stands: a local being declared, an element of an array, an expression being called.
        /// </summary>
        public TypeEntry SettleLambda(NodeBase node, TypeEntry calculatedType = null)
        {
            var type = calculatedType ?? node.Resolve(this);

            if (!type.IsLambdaType() || !(node is LambdaNode lambda))
                return type;

            lambda.CommitToDefaultDelegate(this);
            return lambda.Resolve(this);
        }

        /// <summary>
        /// Checks if the expression returns a value and has a specified type, and returns that type.
        ///
        /// A lambda literal reaching here is settled first: none of the callers names a delegate for
        /// it to become, so the type they go on to use is the one it settles into.
        /// </summary>
        public TypeEntry CheckTypedExpression(NodeBase node, TypeEntry calculatedType = null, bool allowNull = false)
        {
            var type = SettleLambda(node, calculatedType);

            if (!allowNull && type.Is<NullType>())
                Error(node, CompilerMessages.ExpressionNull);

            if (type.IsVoid())
                Error(node, CompilerMessages.ExpressionVoid);

            return type;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Create a type entry without setting its parent info.
        /// </summary>
        private TypeEntity CreateTypeCore(string name, bool isSealed, bool defaultCtor, bool prepare, Action<TypeEntity> extraInit = null)
        {
            if (_definedTypes.ContainsKey(name))
                Error(CompilerMessages.TypeDefined, name);

            var te = new TypeEntity(this)
            {
                Name = name,
                IsSealed = isSealed,
            };
            _definedTypes.Add(name, te);

            extraInit?.Invoke(te);

            if (prepare)
                te.PrepareSelfAsNeeded();
            else
                UnpreparedTypes.Add(te);

            if (defaultCtor)
                te.CreateConstructor(null, prepare);

            return te;
        }

        #endregion
    }
}