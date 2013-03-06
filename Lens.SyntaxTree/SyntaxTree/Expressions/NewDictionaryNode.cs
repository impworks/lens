using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new dictionary.
	/// </summary>
	public class NewDictionaryNode : ValueListNodeBase<KeyValuePair<NodeBase, NodeBase>>, IEnumerable<KeyValuePair<NodeBase, NodeBase>>
	{
		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if(Expressions.Count == 0)
				Error("Use explicit constructor to create an empty dictionary!");

			var keyType = Expressions[0].Key.GetExpressionType(ctx);
			var valType = Expressions[0].Value.GetExpressionType(ctx);

			if (keyType == typeof(NullType))
				Error(Expressions[0].Key, "Cannot infer type of the first record of the directory. Please use casting to specify the type!");

			if (keyType.IsVoid())
				Error(Expressions[0].Key, "An expression that returns a value is expected!");

			if (valType == typeof(NullType))
				Error(Expressions[0].Value, "Cannot infer type of the first record of the directory. Please use casting to specify the type!");

			if (valType.IsVoid())
				Error(Expressions[0].Value, "An expression that returns a value is expected!");

			return typeof(Dictionary<,>).MakeGenericType(keyType, valType);
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			foreach (var curr in Expressions)
			{
				yield return curr.Key;
				yield return curr.Value;
			}
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			var keyType = Expressions[0].Key.GetExpressionType(ctx);
			var valType = Expressions[0].Value.GetExpressionType(ctx);
			var dictType = GetExpressionType(ctx);

			var tmpVar = ctx.CurrentScope.DeclareImplicitName(ctx, dictType, true);

			var ctor = dictType.GetConstructor(new[] {typeof (int)});
			var addMethod = dictType.GetMethod("Add", new[] {keyType, valType});

			var count = Expressions.Count;
			gen.EmitConstant(count);
			gen.EmitCreateObject(ctor);
			gen.EmitSaveLocal(tmpVar);

			foreach (var curr in Expressions)
			{
				var currKeyType = curr.Key.GetExpressionType(ctx);
				var currValType = curr.Value.GetExpressionType(ctx);

				if (currKeyType != keyType)
					Error(curr.Key, "Cannot add a key of type '{0}' to Dictionary<{1}, {2}>", currKeyType, keyType, valType);

				if (currKeyType.IsVoid())
					Error(curr.Key, "An expression that returns a value is expected!");

				if (!valType.IsExtendablyAssignableFrom(currValType))
					Error(curr.Value, "Cannot add a value of type '{0}' to Dictionary<{1}, {2}>", currValType, keyType, valType);

				if (currValType.IsVoid())
					Error(curr.Value, "An expression that returns a value is expected!");

				gen.EmitLoadLocal(tmpVar);

				curr.Key.Compile(ctx, true);
				Expr.Cast(curr.Value, valType).Compile(ctx, true);

				gen.EmitCall(addMethod);
			}

			gen.EmitLoadLocal(tmpVar);
		}

		#region Equality members

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

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion

		public void Add(NodeBase key, NodeBase value)
		{
			Expressions.Add(new KeyValuePair<NodeBase, NodeBase>(key, value));
		}

		public IEnumerator<KeyValuePair<NodeBase, NodeBase>> GetEnumerator()
		{
			return Expressions.GetEnumerator();
		}

		public override string ToString()
		{
			return string.Format("dict({0})", string.Join(";", Expressions.Select(x => string.Format("{0} => {1}", x.Key, x.Value))));
		}
	}
}
