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
		/// Dictionary key types.
		/// Key types are enforced to be strictly equal, no common type is being resolved.
		/// </summary>
		private Type _KeyType;

		/// <summary>
		/// Common type for dictionary's values.
		/// </summary>
		private Type _ValueType;

		#endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			if(Expressions.Count == 0)
				error(CompilerMessages.DictionaryEmpty);

			_KeyType = Expressions[0].Key.Resolve(ctx);
			_ValueType = resolveItemType(Expressions.Select(exp => exp.Value), ctx);

			if (_ValueType == typeof(NullType) || _KeyType == typeof(NullType))
				error(Expressions[0].Value, CompilerMessages.DictionaryTypeUnknown);

			ctx.CheckTypedExpression(Expressions[0].Key, _KeyType);
			ctx.CheckTypedExpression(Expressions[0].Value, _ValueType, true);

			return typeof(Dictionary<,>).MakeGenericType(_KeyType, _ValueType);
		}

		#endregion

		#region Transform

		protected override IEnumerable<NodeChild> getChildren()
		{
			for (var idx = 0; idx < Expressions.Count; idx++)
			{
				var id = idx;
				var curr = Expressions[idx];
				yield return new NodeChild(curr.Key, x => Expressions[id] = new KeyValuePair<NodeBase, NodeBase>(x, curr.Value));
				yield return new NodeChild(curr.Value, x => Expressions[id] = new KeyValuePair<NodeBase, NodeBase>(curr.Key, x));
			}
		}

		#endregion

		#region Emit

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;
			var dictType = Resolve(ctx);

			var tmpVar = ctx.Scope.DeclareImplicit(ctx, dictType, true);

			var ctor = ctx.ResolveConstructor(dictType, new[] {typeof (int)});
			var addMethod = ctx.ResolveMethod(dictType, "Add", new[] { _KeyType, _ValueType });

			var count = Expressions.Count;
			gen.EmitConstant(count);
			gen.EmitCreateObject(ctor.ConstructorInfo);
			gen.EmitSaveLocal(tmpVar.LocalBuilder);

			foreach (var curr in Expressions)
			{
				var currKeyType = curr.Key.Resolve(ctx);
				var currValType = curr.Value.Resolve(ctx);

				ctx.CheckTypedExpression(curr.Key, currKeyType);
				ctx.CheckTypedExpression(curr.Value, currValType, true);

				if (currKeyType != _KeyType)
					error(curr.Key, CompilerMessages.DictionaryKeyTypeMismatch, currKeyType, _KeyType, _ValueType);

				if (!_ValueType.IsExtendablyAssignableFrom(currValType))
					error(curr.Value, CompilerMessages.DictionaryValueTypeMismatch, currValType, _KeyType, _ValueType);

				gen.EmitLoadLocal(tmpVar.LocalBuilder);

				curr.Key.Emit(ctx, true);
				Expr.Cast(curr.Value, _ValueType).Emit(ctx, true);

				gen.EmitCall(addMethod.MethodInfo);
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
			if (obj.GetType() != this.GetType()) return false;
			return Equals((NewDictionaryNode)obj);
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
