using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// A base class for various forms of method invocation that stores arguments.
	/// </summary>
	abstract internal class InvocationNodeBase : NodeBase
	{
		protected InvocationNodeBase()
		{
			Arguments = new List<NodeBase>();
		}

		public List<NodeBase> Arguments { get; set; }

		protected abstract CallableWrapperBase _Wrapper { get; }
		protected Type[] _ArgTypes;

		#region Methods

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var isParameterless = Arguments.Count == 1 && Arguments[0].Resolve(ctx) == typeof(Unit);

			_ArgTypes = isParameterless
				? Type.EmptyTypes
				: Arguments.Select(a => a.Resolve(ctx)).ToArray();

			// prepares arguments only
			return null;
		}

		public override NodeBase Expand(Context ctx, bool mustReturn)
		{
			if (_Wrapper.IsPartiallyApplied)
			{
				// (expr) _ a b _
				// is transformed into
				// (pa0:T1 pa1:T2) -> (expr) (pa0) (a) (b) (pa1)
				var argDefs = new List<FunctionArgument>();
				var argExprs = new List<NodeBase>();
				for (var idx = 0; idx < _ArgTypes.Length; idx++)
				{
					if (_ArgTypes[idx] == null)
					{
						var argName = ctx.Unique.AnonymousArgName();
						argDefs.Add(Expr.Arg(argName, _Wrapper.ArgumentTypes[idx].FullName));
						argExprs.Add(Expr.Get(argName));
					}
					else
					{
						argExprs.Add(Arguments[idx]);
					}
				}

				return Expr.Lambda(argDefs, recreateSelfWithArgs(argExprs));
			}

			if (_Wrapper.IsVariadic)
			{
				var srcTypes = _ArgTypes;
				var dstTypes = _Wrapper.ArgumentTypes;
				var lastDst = dstTypes[dstTypes.Length - 1];
				var lastSrc = srcTypes[srcTypes.Length - 1];

				// compress items into an array:
				//     fx a b c d
				// becomes
				//     fx a b (new[ c as X; d as X ])
				if (dstTypes.Length > srcTypes.Length || lastDst != lastSrc)
				{
					var elemType = lastDst.GetElementType();
					var simpleArgs = Arguments.Take(dstTypes.Length - 1);
					var combined = Expr.Array(Arguments.Skip(dstTypes.Length - 1).Select(x => Expr.Cast(x, elemType)).ToArray());
					return recreateSelfWithArgs(simpleArgs.Union(new[] { combined }));
				}
			}

			return base.Expand(ctx, mustReturn);
		}

		/// <summary>
		/// Creates a similar instance of invocation node descendant with replaced arguments list.
		/// </summary>
		protected abstract InvocationNodeBase recreateSelfWithArgs(IEnumerable<NodeBase> newArgs);

		#endregion

		#region Equality members

		protected bool Equals(InvocationNodeBase other)
		{
			return Arguments.SequenceEqual(other.Arguments);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((InvocationNodeBase)obj);
		}

		public override int GetHashCode()
		{
			return (Arguments != null ? Arguments.GetHashCode() : 0);
		}

		#endregion
	}
}
