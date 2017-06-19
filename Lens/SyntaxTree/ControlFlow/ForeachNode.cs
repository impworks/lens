﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
	internal class ForeachNode : NodeBase
	{
		#region Fields

		/// <summary>
		/// A variable to assign current item to.
		/// </summary>
		public string VariableName { get; set; }

		/// <summary>
		/// Explicitly specified local variable.
		/// </summary>
		public Local Local { get; set; }

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

		private Type _variableType;
		private Type _enumeratorType;
		private PropertyWrapper _currentProperty;

		#endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			if (IterableExpression != null)
				DetectEnumerableType(ctx);
			else
				DetectRangeType(ctx);

			if (VariableName != null && ctx.Scope.FindLocal(VariableName) != null)
				throw new LensCompilerException(string.Format(CompilerMessages.VariableDefined, VariableName));

			if (Local == null)
			{
				// variable must be defined: declare it in a temporary scope for pre-resolve state
				var tmpVar = new Local(VariableName, _variableType);
				return Scope.WithTempLocals(ctx, () => Body.Resolve(ctx, mustReturn), tmpVar);
			}

			// index local specified explicitly: no need to account for it in pre-resolve
			return Body.Resolve(ctx, mustReturn);
		}

		#endregion

		#region Transform

		protected override NodeBase Expand(Context ctx, bool mustReturn)
		{
			if (IterableExpression != null)
			{
				var type = IterableExpression.Resolve(ctx);
				if (type.IsArray)
					return ExpandArray(ctx);
				
				return ExpandEnumerable(ctx, mustReturn);
			}

			return ExpandRange(ctx);
		}

		protected override IEnumerable<NodeChild> GetChildren()
		{
			if (IterableExpression != null)
			{
				yield return new NodeChild(IterableExpression, x => IterableExpression = x);
			}
			else
			{
				yield return new NodeChild(RangeStart, x => RangeStart = x);
				yield return new NodeChild(RangeEnd, x => RangeEnd = x);
			}

			yield return new NodeChild(Body, null);
		}

		/// <summary>
		/// Expands the foreach loop if it iterates over an IEnumerable`1.
		/// </summary>
		private NodeBase ExpandEnumerable(Context ctx, bool mustReturn)
		{
			var iteratorVar = ctx.Scope.DeclareImplicit(ctx, _enumeratorType, false);
			var enumerableType = _enumeratorType.IsGenericType
				? typeof (IEnumerable<>).MakeGenericType(_enumeratorType.GetGenericArguments()[0])
				: typeof (IEnumerable);

			var init = Expr.Set(
				iteratorVar,
				Expr.Invoke(
					Expr.Cast(IterableExpression, enumerableType),
					"GetEnumerator"
				)
			);

			var loop = Expr.While(
				Expr.Invoke(Expr.Get(iteratorVar), "MoveNext"),
				Expr.Block(
					GetIndexAssignment(Expr.GetMember(Expr.Get(iteratorVar), "Current")),
					Body
				)
			);

			if (_enumeratorType.Implements(typeof (IDisposable), false))
			{
				var dispose = Expr.Block(Expr.Invoke(Expr.Get(iteratorVar), "Dispose"));
				var returnType = Resolve(ctx);
				var saveLast = mustReturn && !returnType.IsVoid();

				if (saveLast)
				{
					var resultVar = ctx.Scope.DeclareImplicit(ctx, _enumeratorType, false);
					return Expr.Block(
						Expr.Try(
							Expr.Block(
								init,
								Expr.Set(resultVar, loop)
							),
							dispose
						),
						Expr.Get(resultVar)
					);
				}

				return Expr.Try(
					Expr.Block(init, loop),
					dispose
				);
			}

			return Expr.Block(
				init,
				loop
			);
		}

		/// <summary>
		/// Expands the foreach loop if it iterates over T[].
		/// </summary>
		private NodeBase ExpandArray(Context ctx)
		{
			var arrayVar = ctx.Scope.DeclareImplicit(ctx, IterableExpression.Resolve(ctx), false);
			var idxVar = ctx.Scope.DeclareImplicit(ctx, typeof(int), false);
			var lenVar = ctx.Scope.DeclareImplicit(ctx, typeof(int), false);

			return Expr.Block(
				Expr.Set(idxVar, Expr.Int(0)),
				Expr.Set(arrayVar, IterableExpression),
				Expr.Set(lenVar, Expr.GetMember(Expr.Get(arrayVar), "Length")),
				Expr.While(
					Expr.Less(
						Expr.Get(idxVar),
						Expr.Get(lenVar)
					),
					Expr.Block(
						GetIndexAssignment(
							Expr.GetIdx(
								Expr.Get(arrayVar),
								Expr.Get(idxVar)
							)
						),
						Expr.Set(
							idxVar,
							Expr.Add(Expr.Get(idxVar), Expr.Int(1))
						),
						Body
					)
				)
			);
		}

		/// <summary>
		/// Expands the foreach loop if it iterates over a numeric range.
		/// </summary>
		private NodeBase ExpandRange(Context ctx)
		{
			var signVar = ctx.Scope.DeclareImplicit(ctx, _variableType, false);
			var idxVar = ctx.Scope.DeclareImplicit(ctx, _variableType, false);

			return Expr.Block(
				Expr.Set(idxVar, RangeStart),
				Expr.Set(
					signVar,
					Expr.Invoke(
						"Math",
						"Sign",
						Expr.Sub(RangeEnd, Expr.Get(idxVar))
					)
				),
				Expr.While(
					Expr.NotEqual(Expr.Get(idxVar), RangeEnd),
					Expr.Block(
						GetIndexAssignment(Expr.Get(idxVar)),
						Body,
						Expr.Set(
							idxVar,
							Expr.Add(
								Expr.Get(idxVar),
								Expr.Get(signVar)
							)
						)
					)
				)
			);
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Calculates the variable type and other required values for enumeration of an IEnumerable`1.
		/// </summary>
		private void DetectEnumerableType(Context ctx)
		{
			var seqType = IterableExpression.Resolve(ctx);
			if (seqType.IsArray)
			{
				_variableType = seqType.GetElementType();
				return;
			}

			var ifaces = seqType.ResolveInterfaces();
			if (seqType.IsInterface)
				ifaces = ifaces.Union(new[] { seqType }).ToArray();

			var generic = ifaces.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
			if (generic != null)
				_enumeratorType = typeof(IEnumerator<>).MakeGenericType(generic.GetGenericArguments()[0]);

			else if (ifaces.Contains(typeof(IEnumerable)))
				_enumeratorType = typeof(IEnumerator);

			else
				Error(IterableExpression, CompilerMessages.TypeNotIterable, seqType);

			_currentProperty = ctx.ResolveProperty(_enumeratorType, "Current");
			_variableType = _currentProperty.PropertyType;
		}

		/// <summary>
		/// Calculates the variable type of a numeric range iteration.
		/// </summary>
		private void DetectRangeType(Context ctx)
		{
			var t1 = RangeStart.Resolve(ctx);
			var t2 = RangeEnd.Resolve(ctx);

			if (t1 != t2)
				Error(CompilerMessages.ForeachRangeTypeMismatch, t1, t2);

			if (!t1.IsIntegerType())
				Error(CompilerMessages.ForeachRangeNotInteger, t1);

			_variableType = t1;
		}

		/// <summary>
		/// Gets the expression for saving the value at an index to a variable.
		/// </summary>
		private NodeBase GetIndexAssignment(NodeBase indexGetter)
		{
			return Local == null
				? Expr.Let(VariableName, indexGetter)
				: Expr.Set(Local, indexGetter) as NodeBase;
		}

		#endregion

		#region Debug

		protected bool Equals(ForeachNode other)
		{
			return string.Equals(VariableName, other.VariableName)
			       && Equals(IterableExpression, other.IterableExpression)
			       && Equals(RangeStart, other.RangeStart)
				   && Equals(RangeEnd, other.RangeEnd)
				   && Equals(Body, other.Body);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((ForeachNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = (VariableName != null ? VariableName.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (IterableExpression != null ? IterableExpression.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (RangeStart != null ? RangeStart.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (RangeEnd != null ? RangeEnd.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (Body != null ? Body.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion
	}
}
