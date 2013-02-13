using System;
using System.Collections.Generic;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a new array declaration.
	/// </summary>
	public class NewArrayNode : ValueListNodeBase<NodeBase>
	{
		/// <summary>
		/// Temp variable used to instantiate the array.
		/// </summary>
		private string _TempVariable;

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if(Expressions.Count == 0)
				Error("Array must contain at least one object!");

			return Expressions[0].GetExpressionType(ctx).MakeArrayType();
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			return Expressions;
		}

		public override void ProcessClosures(Context ctx)
		{
			base.ProcessClosures(ctx);

			_TempVariable = ctx.CurrentScope.DeclareImplicitName(GetExpressionType(ctx), true).Name;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;
			var itemType = Expressions[0].GetExpressionType(ctx);
			var varId = ctx.CurrentScope.FindName(_TempVariable).LocalId.Value;

			// create array
			var count = Expressions.Count;
			gen.EmitConstant(count);
			gen.EmitCreateArray(itemType);
			gen.EmitSaveLocal(varId);

			for (var idx = 0; idx < count; idx++)
			{
				var currType = Expressions[idx].GetExpressionType(ctx);
				if(currType != itemType)
					Error("Cannot add object of type '{0}' to array of type '{1}': item types must match exactly.", currType, itemType);

				gen.EmitLoadLocal(varId);
				gen.EmitConstant(idx);

				if (itemType.IsValueType)
				{
					gen.EmitLoadIndexAddress(itemType);
					Expressions[idx].Compile(ctx, true);
					gen.EmitSaveObject(itemType);
				}
				else
				{
					Expressions[idx].Compile(ctx, true);
					gen.EmitSaveIndex(itemType);
				}
			}

			gen.EmitLoadLocal(varId);
		}

		public override string ToString()
		{
			return string.Format("array({0})", string.Join(";", Expressions));
		}
	}
}
