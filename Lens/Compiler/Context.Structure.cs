using System;
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
        internal TypeEntity CreateType(string name, string parent = null, bool isSealed = false, bool defaultCtor = true, bool prepare = true)
        {
            return CreateTypeCore(name, isSealed, defaultCtor, prepare, x => x.ParentSignature = parent);
        }

        /// <summary>
        /// Checks if the expression returns a value and has a specified type.
        /// </summary>
        public void CheckTypedExpression(NodeBase node, Type calculatedType = null, bool allowNull = false)
        {
            var type = calculatedType ?? node.Resolve(this);

            if (!allowNull && type == typeof(NullType))
                Error(node, CompilerMessages.ExpressionNull);

            if (type.IsVoid())
                Error(node, CompilerMessages.ExpressionVoid);

            if (type.IsLambdaType())
            {
                var argUnknown = (node as LambdaNode).Arguments.First(x => x.Type == typeof(UnspecifiedType));
                Error(node, CompilerMessages.LambdaArgTypeUnknown, argUnknown.Name);
            }
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
                te.PrepareSelf();
            else
                UnpreparedTypes.Add(te);

            if (defaultCtor)
                te.CreateConstructor(null, prepare);

            return te;
        }

        #endregion
    }
}