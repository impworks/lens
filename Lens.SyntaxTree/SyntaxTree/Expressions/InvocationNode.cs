using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Literals;
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

		private NodeBase m_InvocationSource;
		private MethodInfo m_Method;

		private Type[] m_ArgTypes;
		private Type[] m_TypeHints;

		#region Resolve
		
		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			if (!m_IsResolved)
				resolve(ctx);

			return m_Method.ReturnType;
		}

		private void resolve(Context ctx)
		{
			var isParameterless = Arguments.Count == 1 && Arguments[0].GetExpressionType(ctx) == typeof(Unit);

			m_ArgTypes = isParameterless
				? Type.EmptyTypes
				: Arguments.Select(a => a.GetExpressionType(ctx)).ToArray();

			if (Expression is GetMemberNode)
				resolveGetMember(ctx, Expression as GetMemberNode);
			else if (Expression is GetIdentifierNode)
				resolveGetIdentifier(ctx, Expression as GetIdentifierNode);
			else
				resolveExpression(ctx, Expression);

			m_Method = GenericHelper.ResolveMethodGenerics(m_Method, m_ArgTypes, m_TypeHints);

			m_IsResolved = true;
		}

		private void resolveGetMember(Context ctx, GetMemberNode node)
		{
			m_InvocationSource = node.Expression;
			var type = m_InvocationSource != null
				? m_InvocationSource.GetExpressionType(ctx)
				: ctx.ResolveType(node.StaticType);

			if (node.TypeHints.Any())
				m_TypeHints = node.TypeHints.Select(x => x.Signature == "_" ? null : ctx.ResolveType(x)).ToArray();

			try
			{
				try
				{
					m_Method = ctx.ResolveMethod(type, node.MemberName, m_ArgTypes);
				}
				catch (KeyNotFoundException)
				{
					if (m_InvocationSource == null)
						throw;

					m_Method = type.FindExtensionMethod(node.MemberName, m_ArgTypes);
					
					// move invocation source to arguments
					if (Arguments[0] is UnitNode)
						Arguments[0] = m_InvocationSource;
					else
						Arguments.Insert(0, m_InvocationSource);

					m_ArgTypes = new[] {m_InvocationSource.GetExpressionType(ctx)}.Union(m_ArgTypes).ToArray();
					m_InvocationSource = null;
				}
			}
			catch (AmbiguousMatchException)
			{
				Error("Type '{0}' has more than one suitable override of '{1}'! Please use type casting to specify the exact override.", type, node.MemberName);
			}
			catch (KeyNotFoundException)
			{
				Error("Type '{0}' has no method named '{1}' and no extension method accepting given arguments was found!", type, node.MemberName);
			}
		}

		private void resolveGetIdentifier(Context ctx, GetIdentifierNode node)
		{
			var nameInfo = ctx.CurrentScope.FindName(node.Identifier);
			if (nameInfo != null)
			{
				resolveExpression(ctx, node);
				return;
			}

			try
			{
				m_Method = ctx.MainType.ResolveMethod(node.Identifier, m_ArgTypes);
				if (m_Method == null)
					throw new KeyNotFoundException();
			}
			catch (KeyNotFoundException)
			{
				Error("No global function named '{0}' with suitable arguments is declared!", node.Identifier);
			}
			catch (AmbiguousMatchException)
			{
				Error("There is more than one suitable override of global method '{0}'! Please use type casting to specify the exact override.", node.Identifier);
			}
		}

		private void resolveExpression(Context ctx, NodeBase node)
		{
			var exprType = node.GetExpressionType(ctx);
			if (!exprType.IsCallableType())
				Error("Type '{0}' cannot be invoked!");

			var argTypes = exprType.GetArgumentTypes();
			if (argTypes.Length != m_ArgTypes.Length)
				Error("Invoking a '{0}' requires {1} arguments, {2} given instead!", exprType, argTypes.Length, m_ArgTypes.Length);

			for (var idx = 0; idx < argTypes.Length; idx++)
			{
				var fromType = m_ArgTypes[idx];
				var toType = argTypes[idx];
				if (!toType.IsExtendablyAssignableFrom(fromType))
					Error(Arguments[idx], "Cannot use object of type '{0}' as a value for parameter of type '{1}'!", fromType, toType);
			}

			m_InvocationSource = node;
			m_Method = exprType.GetMethod("Invoke", m_ArgTypes);
		}

		#endregion

		public override IEnumerable<NodeBase> GetChildNodes()
		{
			if (Expression != null)
				yield return Expression;

			foreach (var curr in Arguments)
				yield return curr;
		}

		#region Compile
		
		public override void Compile(Context ctx, bool mustReturn)
		{
			if (!m_IsResolved)
				resolve(ctx);

			var gen = ctx.CurrentILGenerator;

			Type constraint = null;

			if (m_InvocationSource != null)
			{
				var type = m_InvocationSource.GetExpressionType(ctx);

				if (type.IsValueType)
				{
					constraint = type;
					if (m_InvocationSource is IPointerProvider)
					{
						(m_InvocationSource as IPointerProvider).PointerRequired = true;
						m_InvocationSource.Compile(ctx, true);
					}
					else
					{
						var tmpVar = ctx.CurrentScope.DeclareImplicitName(ctx, type, true);
						gen.EmitLoadLocal(tmpVar, true);

						m_InvocationSource.Compile(ctx, true);
						gen.EmitSaveObject(type);
						
						gen.EmitLoadLocal(tmpVar, true);
					}
				}
				else
				{
					m_InvocationSource.Compile(ctx, true);
				}
			}

			if (m_ArgTypes.Length > 0)
			{
				var toTypes = m_Method is MethodBuilder
					? ctx.FindMethod(m_Method).GetArgumentTypes(ctx)
					: m_Method.GetParameters().Select(p => p.ParameterType).ToArray();

				for (var idx = 0; idx < Arguments.Count; idx++)
					Expr.Cast(Arguments[idx], toTypes[idx]).Compile(ctx, true);
			}

			if (constraint != null)
				gen.EmitCall(m_Method, true, constraint);	
			else
				gen.EmitCall(m_Method);
		}

		#endregion

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
			return string.Format("invoke({0}, args: {1})", Expression, string.Join(",", Arguments));
		}
	}
}
