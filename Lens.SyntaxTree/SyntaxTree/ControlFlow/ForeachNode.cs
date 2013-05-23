using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	public class ForeachNode : NodeBase
	{
		/// <summary>
		/// A variable to assign current item to.
		/// </summary>
		public string VariableName { get; set; }

		/// <summary>
		/// A single expression of iterable type.
		/// Must be set to X if the loop is defined like (for A in X)
		/// </summary>
		public NodeBase IterableExpression { get; set; }

		/// <summary>
		/// The lower limit of loop range.
		/// Must be set to X if the loop is defined like (for A in X..Y)
		/// </summary>
		public NodeBase RangeStart { get; set; }

		/// <summary>
		/// The upper limit of loop range.
		/// Must be set to Y if the loop is defined like (for A in X..Y)
		/// </summary>
		public NodeBase RangeEnd { get; set; }

		public CodeBlockNode Body { get; set; }

		public override LexemLocation EndLocation
		{
			get { return Body.EndLocation; }
			set { LocationSetError(); }
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			return mustReturn ? Body.GetExpressionType(ctx) : typeof(Unit);
		}

		public override void ProcessClosures(Context ctx)
		{
			base.ProcessClosures(ctx);

			var type = IterableExpression != null
				? getEnumerableType(ctx)
				: getRangeType(ctx);

			ctx.CurrentScope.DeclareName(VariableName, type, false);
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			if (IterableExpression != null)
			{
				yield return IterableExpression;
			}
			else
			{
				yield return RangeStart;
				yield return RangeEnd;
			}

			yield return Body;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		private Type getEnumerableType(Context ctx)
		{
			var ifaces = GenericHelper.GetInterfaces(IterableExpression.GetExpressionType(ctx));
			var generics = ifaces.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)).ToArray();

			if (generics.Length == 0)
				Error("");

			if (generics.Length == 1)
				return generics[0].GetGenericArguments()[0];

			if (!ifaces.Contains(typeof (IEnumerable)))
				Error("");

			return typeof (object);
		}

		private Type getRangeType(Context ctx)
		{
			var t1 = RangeStart.GetExpressionType(ctx);
			var t2 = RangeEnd.GetExpressionType(ctx);

			if(t1 != t2)
				Error("");

			if(!t1.IsIntegerType())
				Error("");

			return t1;
		}
	}
}
