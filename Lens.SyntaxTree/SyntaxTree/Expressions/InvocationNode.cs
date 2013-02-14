using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Operators;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a method being invoked.
	/// </summary>
	public class InvocationNode : InvocationNodeBase
	{
		/// <summary>
		/// An expression to invoke the method on.
		/// </summary>
		public NodeBase Expression { get; set; }

		private bool m_IsResolved;
		private bool m_IsGlobalMethod;
		private LocalName m_LocalVariable;
		private MethodInfo m_Method;

		private Type[] m_ArgTypes;

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if (!m_IsResolved)
				resolve(ctx);

			return m_Method.ReturnType;
		}

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			if (Expression != null)
				yield return Expression;

			foreach (var curr in Arguments)
				yield return curr;
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			if (Expression is GetMemberNode)
			{
				
			}

			else if (Expression is GetIdentifierNode)
			{

			}

			else
			{
				Expression.Compile(ctx, true);
				compileArgs(ctx);
				gen.EmitCall(m_Method);
			}
		}

		private void resolve(Context ctx)
		{
			m_ArgTypes = Arguments.Select(x => x.GetExpressionType(ctx)).ToArray();

			if (Expression is GetMemberNode)
				resolveGetMember(ctx, Expression as GetMemberNode);
			else if(Expression is GetIdentifierNode)
				resolveGetIdentifier(ctx, Expression as GetIdentifierNode);
			else
				resolveExpression(ctx, Expression);

			m_IsResolved = true;
		}

		private void resolveGetMember(Context ctx, GetMemberNode node)
		{

		}

		private void resolveGetIdentifier(Context ctx, GetIdentifierNode node)
		{
			
		}

		private void resolveExpression(Context ctx, NodeBase node)
		{
			var exprType = node.GetExpressionType(ctx);
			if(!exprType.IsCallableType())
				Error("Type '{0}' cannot be invoked!");

			var argTypes = exprType.GetArgumentTypes();
			if(argTypes.Length != m_ArgTypes.Length)
				Error("Invoking a '{0}' requires {1} arguments, {2} given instead.", exprType, argTypes.Length, m_ArgTypes.Length);

			for (var idx = 0; idx < argTypes.Length; idx++)
			{
				var fromType = m_ArgTypes[idx];
				var toType = argTypes[idx];
				if(!toType.IsExtendablyAssignableFrom(fromType))
					Error(Arguments[idx], "Cannot use object of type '{0}' as a value for parameter of type '{1}'!", fromType, toType);
			}

			m_Method = exprType.GetMethod("Invoke", m_ArgTypes);
			if(m_Method == null)
				Error("Method could not be resolved.");
		}

		private void compileArgs(Context ctx)
		{
			var toTypes = m_Method.GetParameters().Select(p => p.ParameterType).ToArray();
			for (var idx = 0; idx < Arguments.Count; idx++)
			{
				var castNode = new CastOperatorNode
				{
					Expression = Arguments[idx],
					Type = toTypes[idx]
				};

				castNode.Compile(ctx, true);
			}
		}

		#region Equality members

		protected bool Equals(InvocationNode other)
		{
			return base.Equals(other)
				&& Equals(Expression, other.Expression);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((InvocationNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				int hashCode = base.GetHashCode();
				hashCode = (hashCode * 397) ^ (Expression != null ? Expression.GetHashCode() : 0);
				return hashCode;
			}
		}

		#endregion

		public override string ToString()
		{
			return string.Format("invoke({0}, args: ({1}))", Expression, string.Join(",", Arguments));
		}
	}
}
