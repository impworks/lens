using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.Instantiation
{
    /// <summary>
    /// A node representing a new tuple declaration.
    /// </summary>
    internal class NewTupleNode : CollectionNodeBase<NodeBase>
    {
        #region Fields

        /// <summary>
        /// List of tuple item types.
        /// </summary>
        private Type[] _types;

        #endregion

        #region Resolve

        protected override Type resolve(Context ctx, bool mustReturn)
        {
            if (Expressions.Count == 0)
                Error(CompilerMessages.TupleNoArgs);

            if (Expressions.Count > 8)
                Error(CompilerMessages.TupleTooManyArgs);

            var types = new List<Type>();
            foreach (var curr in Expressions)
            {
                var type = curr.Resolve(ctx);
                ctx.CheckTypedExpression(curr, type);

                types.Add(type);
            }

            _types = types.ToArray();
            return FunctionalHelper.CreateTupleType(_types);
        }

        #endregion

        #region Transform

        protected override IEnumerable<NodeChild> GetChildren()
        {
            return Expressions.Select((expr, i) => new NodeChild(expr, x => Expressions[i] = x));
        }

        #endregion

        #region Emit

        protected override void EmitCode(Context ctx, bool mustReturn)
        {
            var tupleType = Resolve(ctx);

            var gen = ctx.CurrentMethod.Generator;

            foreach (var curr in Expressions)
                curr.Emit(ctx, true);

            var ctor = ctx.ResolveConstructor(tupleType, _types);
            gen.EmitCreateObject(ctor.ConstructorInfo);
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return string.Format("tuple({0})", string.Join(";", Expressions));
        }

        #endregion
    }
}