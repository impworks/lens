using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.Instantiation
{
    /// <summary>
    /// A node representing a new dictionary.
    /// </summary>
    internal class NewDictionaryNode : CollectionNodeBase<KeyValuePair<NodeBase, NodeBase>>, IEnumerable<KeyValuePair<NodeBase, NodeBase>>
    {
        #region Fields

        /// <summary>
        /// Dictionary key type.
        /// Actual types are enforced to be strictly equal, no common type is being resolved.
        /// </summary>
        private TypeEntry _keyType;

        /// <summary>
        /// Common type inferred from all pair values' actual types.
        /// </summary>
        private TypeEntry _valueType;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            if (Expressions.Count == 0)
                Error(CompilerMessages.DictionaryEmpty);

            _keyType = Expressions[0].Key.Resolve(ctx);
            _valueType = ResolveItemType(Expressions.Select(exp => exp.Value), ctx);

            if (_valueType.Is<NullType>() || _keyType.Is<NullType>())
                Error(Expressions[0].Value, CompilerMessages.DictionaryTypeUnknown);

            // every element is checked here rather than while emitting, because an editor binds
            // the tree and never emits: a check that lives in EmitInternal is one the reader of a
            // half-written script never sees
            foreach (var curr in Expressions)
            {
                var currKeyType = ctx.CheckTypedExpression(curr.Key);
                var currValType = ctx.CheckTypedExpression(curr.Value, allowNull: true);

                if (currKeyType != _keyType)
                    Error(curr.Key, CompilerMessages.DictionaryKeyTypeMismatch, currKeyType, _keyType, _valueType);

                if (!_valueType.IsExtendablyAssignableFrom(ctx.Resolver, currValType))
                    Error(curr.Value, CompilerMessages.DictionaryValueTypeMismatch, currValType, _keyType, _valueType);
            }

            return TypeEntryCache.Of(typeof(Dictionary<,>)).MakeGeneric(ctx.Resolver, new[] {_keyType, _valueType});
        }

        #endregion

        #region Transform

        internal override IEnumerable<NodeChild> GetChildren()
        {
            for (var idx = 0; idx < Expressions.Count; idx++)
            {
                var id = idx;
                var curr = Expressions[idx];
                yield return new NodeChild(curr.Key);
                yield return new NodeChild(curr.Value);
            }
        }

        // each pair is filled in before the next one, key first
        internal override IReadOnlyList<NodeBase> Operands => Expressions.SelectMany(x => new[] {x.Key, x.Value}).ToList();

        internal override NodeBase WithOperands(IReadOnlyList<NodeBase> operands)
        {
            var copy = Copy<NewDictionaryNode>();
            copy.Expressions = new List<KeyValuePair<NodeBase, NodeBase>>();
            for (var idx = 0; idx < operands.Count; idx += 2)
                copy.Expressions.Add(new KeyValuePair<NodeBase, NodeBase>(operands[idx], operands[idx + 1]));

            return copy;
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            var dictType = Resolve(ctx);

            var tmpVar = ctx.Scope.DeclareImplicit(ctx, dictType, true);

            var ctor = ctx.ResolveConstructor(dictType, new[] {TypeEntryCache.Of<int>()});
            var addMethod = ctx.ResolveMethod(dictType, "Add", new[] {_keyType, _valueType});

            var count = Expressions.Count;
            gen.EmitConstant(count);
            gen.EmitCreateObject(ctor.ConstructorInfo);
            gen.EmitSaveLocal(tmpVar.LocalBuilder);

            foreach (var curr in Expressions)
            {
                gen.EmitLoadLocal(tmpVar.LocalBuilder);

                curr.Key.Emit(ctx, true);
                Expr.Cast(curr.Value, _valueType.Materialize()).Emit(ctx, true);

                gen.EmitCall(addMethod.MethodInfo, addMethod.IsVirtual);
            }

            gen.EmitLoadLocal(tmpVar.LocalBuilder);
        }

        #endregion

        #region Debug

        protected bool Equals(NewDictionaryNode other)
        {
            // KeyValuePair doesn't have Equals overridden, that's why it's so messy here:
            return Expressions.Select(e => e.Key).SequenceEqual(other.Expressions.Select(e => e.Key))
                   && Expressions.Select(e => e.Value).SequenceEqual(other.Expressions.Select(e => e.Value));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((NewDictionaryNode) obj);
        }

        public override int GetHashCode()
        {
            return (Expressions != null ? Expressions.GetHashCode() : 0);
        }

        public override string ToString()
        {
            return string.Format("dict({0})", string.Join(";", Expressions.Select(x => string.Format("{0} => {1}", x.Key, x.Value))));
        }

        #endregion

        #region Interface implementations

        /// <summary>
        /// Collection initializer (used in tests).
        /// </summary>
        public void Add(NodeBase key, NodeBase value)
        {
            Expressions.Add(new KeyValuePair<NodeBase, NodeBase>(key, value));
        }

        public IEnumerator<KeyValuePair<NodeBase, NodeBase>> GetEnumerator()
        {
            return Expressions.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}