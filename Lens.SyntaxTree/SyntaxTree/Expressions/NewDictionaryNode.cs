using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Operators;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new dictionary.
	/// </summary>
	public class NewDictionaryNode : ValueListNodeBase<KeyValuePair<NodeBase, NodeBase>>
	{
		/// <summary>
		/// Temp variable used to instantiate the array.
		/// </summary>
		private string _TempVariable;

		protected override Type resolveExpressionType(Context ctx)
		{
			if(Expressions.Count == 0)
				Error("Use explicit constructor to create an empty dictionary!");

			return typeof(Dictionary<,>).MakeGenericType(
				Expressions[0].Key.GetExpressionType(ctx),
				Expressions[0].Value.GetExpressionType(ctx)
			);
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			foreach (var curr in Expressions)
			{
				yield return curr.Key;
				yield return curr.Value;
			}
		}

		public override void ProcessClosures(Context ctx)
		{
			base.ProcessClosures(ctx);

			_TempVariable = ctx.CurrentScope.DeclareImplicitName(GetExpressionType(ctx), true).Name;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			var keyType = Expressions[0].Key.GetExpressionType(ctx);
			var valType = Expressions[0].Value.GetExpressionType(ctx);
			var dictType = GetExpressionType(ctx);
			var varId = ctx.CurrentScope.FindName(_TempVariable).LocalId.Value;

			var ctor = dictType.GetConstructor(new[] {typeof (int)});
			var addMethod = dictType.GetMethod("Add", new[] {keyType, valType});

			var count = Expressions.Count;
			gen.EmitConstant(count);
			gen.EmitCreateObject(ctor);
			gen.EmitSaveLocal(varId);

			foreach (var curr in Expressions)
			{
				var currKeyType = curr.Key.GetExpressionType(ctx);
				var currValType = curr.Value.GetExpressionType(ctx);

				if(currKeyType != keyType)
					Error("Cannot add a key of type '{0}' to Dictionary<{1}, {2}>", currKeyType, keyType, valType);

				if(!valType.IsExtendablyAssignableFrom(currValType))
					Error("Cannot add a value of type '{0}' to Dictionary<{1}, {2}>", currValType, keyType, valType);

				gen.EmitLoadLocal(varId);

				var cast = new CastOperatorNode
				{
					Expression = curr.Value,
					Type = valType
				};

				curr.Key.Compile(ctx, true);
				cast.Compile(ctx, true);

				gen.EmitCall(addMethod);
			}

			gen.EmitLoadLocal(varId);
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

		#endregion

		public override string ToString()
		{
			return string.Format("dict({0})", string.Join(";", Expressions.Select(x => string.Format("{0} => {1}", x.Key, x.Value))));
		}
	}
}
