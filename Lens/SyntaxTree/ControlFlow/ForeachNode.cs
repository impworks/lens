using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
	internal class ForeachNode : NodeBase
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

		private LocalName m_Variable;
		private Type m_VariableType;
		private Type m_EnumeratorType;
		private PropertyWrapper m_CurrentProperty;

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

			if(IterableExpression != null)
				detectEnumerableType(ctx);
			else
				detectRangeType(ctx);

			m_Variable = ctx.CurrentScope.DeclareName(VariableName, m_VariableType, false);
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

		protected override void compile(Context ctx, bool mustReturn)
		{
			if (IterableExpression != null)
			{
				var type = IterableExpression.GetExpressionType(ctx);
				if (type.IsArray)
					compileArray(ctx, mustReturn);
				else
					compileEnumerable(ctx, mustReturn);
			}
			else
			{
				compileRange(ctx, mustReturn);
			}
		}

		private void compileEnumerable(Context ctx, bool mustReturn)
		{
			var returnType = GetExpressionType(ctx);
			var saveLast = mustReturn && !returnType.IsVoid();

			var tmpVar = ctx.CurrentScope.DeclareImplicitName(ctx, m_EnumeratorType, false);
			Expr.Set(tmpVar, Expr.Invoke(IterableExpression, "GetEnumerator")).Compile(ctx, false);

			LocalName result = null;

			var loopWrapper = Expr.Block();
			var loop = Expr.While(
				Expr.Invoke(Expr.Get(tmpVar), "MoveNext"),
				Expr.Block(
					Expr.Set(
						m_Variable,
						Expr.GetMember(Expr.Get(tmpVar), "Current")
					),
					Body
				)
			);

			if (saveLast)
			{
				result = ctx.CurrentScope.DeclareImplicitName(ctx, returnType, false);
				loopWrapper.Add(Expr.Set(result, loop));
			}
			else
			{
				loopWrapper.Add(loop);
			}

			if (m_EnumeratorType.Implements(typeof (IDisposable), false))
			{
				var block = Expr.Try(
					loopWrapper,
					Expr.Block(Expr.Invoke(Expr.Get(tmpVar), "Dispose"))
				);
				block.Compile(ctx, mustReturn);
			}
			else
			{
				loopWrapper.Compile(ctx, mustReturn);
			}

			if (saveLast)
			{
				var gen = ctx.CurrentILGenerator;
				gen.EmitLoadLocal(result);
			}
		}

		private void compileArray(Context ctx, bool mustReturn)
		{
			var returnType = GetExpressionType(ctx);
			var saveLast = mustReturn && !returnType.IsVoid();

			var arrayVar = ctx.CurrentScope.DeclareImplicitName(ctx, IterableExpression.GetExpressionType(ctx), false);
			var idxVar = ctx.CurrentScope.DeclareImplicitName(ctx, typeof (int), false);
			var lenVar = ctx.CurrentScope.DeclareImplicitName(ctx, typeof (int), false);

			LocalName result = null;

			var code = Expr.Block(
				Expr.Set(arrayVar, IterableExpression),
				Expr.Set(lenVar, Expr.GetMember(Expr.Get(arrayVar), "Length"))
			);

			var loop = Expr.While(
				Expr.Less(
					Expr.Get(idxVar),
					Expr.Get(lenVar)
				),
				Expr.Block(
					Expr.Set(
						m_Variable,
						Expr.GetIdx(Expr.Get(arrayVar), Expr.Get(idxVar))
					),
					Expr.Set(
						idxVar,
						Expr.Add(Expr.Get(idxVar), Expr.Int(1))
					),
					Body
				)
			);

			if (saveLast)
			{
				result = ctx.CurrentScope.DeclareImplicitName(ctx, returnType, false);
				code.Add(Expr.Set(result, loop));
			}
			else
			{
				code.Add(loop);
			}

			code.Compile(ctx, mustReturn);

			if (saveLast)
			{
				var gen = ctx.CurrentILGenerator;
				gen.EmitLoadLocal(result);
			}
		}

		private void compileRange(Context ctx, bool mustReturn)
		{
			var signVar = ctx.CurrentScope.DeclareImplicitName(ctx, m_Variable.Type, false);
			var code = Expr.Block(
				Expr.Set(m_Variable, RangeStart),
				Expr.Set(
					signVar,
					Expr.Invoke(
						"Math",
						"Sign",
						Expr.Sub(RangeEnd, Expr.Get(m_Variable))
					)
				),
				Expr.While(
					Expr.If(
						Expr.Equal(Expr.Get(signVar), Expr.Int(1)),
						Expr.Block(Expr.LessEqual(Expr.Get(m_Variable), RangeEnd)),
						Expr.Block(Expr.GreaterEqual(Expr.Get(m_Variable), RangeEnd))
					),
					Expr.Block(
						Body,
						Expr.Set(
							m_Variable,
							Expr.Add(
								Expr.Get(m_Variable),
								Expr.Get(signVar)
							)
						)
					)
				)
			);

			code.Compile(ctx, mustReturn);
		}

		private void detectEnumerableType(Context ctx)
		{
			var seqType = IterableExpression.GetExpressionType(ctx);
			if (seqType.IsArray)
			{
				m_VariableType = seqType.GetElementType();
				return;
			}
			
			var ifaces = GenericHelper.GetInterfaces(seqType);
			if(!ifaces.Any(i => i == typeof(IEnumerable) || (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))))
				Error(CompilerMessages.TypeNotIterable, seqType);

			var enumerator = ctx.ResolveMethod(seqType, "GetEnumerator");
			m_EnumeratorType = enumerator.ReturnType;
			m_CurrentProperty = ctx.ResolveProperty(m_EnumeratorType, "Current");

			m_VariableType = m_CurrentProperty.PropertyType;
		}

		private void detectRangeType(Context ctx)
		{
			var t1 = RangeStart.GetExpressionType(ctx);
			var t2 = RangeEnd.GetExpressionType(ctx);

			if(t1 != t2)
				Error(CompilerMessages.ForeachRangeTypeMismatch, t1, t2);

			if(!t1.IsIntegerType())
				Error(CompilerMessages.ForeachRangeNotInteger, t1);

			m_VariableType = t1;
		}

		#region Equality members

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
