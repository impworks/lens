using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Literals;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing a method being invoked.
	/// </summary>
	internal class InvocationNode : InvocationNodeBase
	{
		/// <summary>
		/// An expression to invoke the method on.
		/// </summary>
		public NodeBase Expression { get; set; }

		private NodeBase _InvocationSource;
		private MethodWrapper _Method;
		protected override CallableWrapperBase _Wrapper { get { return _Method; } }

		private Type[] _TypeHints;

		#region Resolve
		
		protected override Type resolve(Context ctx, bool mustReturn)
		{
			// resolve _ArgTypes
			base.resolve(ctx, mustReturn);

			if (Expression is GetMemberNode)
				resolveGetMember(ctx, Expression as GetMemberNode);
			else if (Expression is GetIdentifierNode)
				resolveGetIdentifier(ctx, Expression as GetIdentifierNode);
			else
				resolveExpression(ctx, Expression);

			applyLambdaArgTypes(ctx);

			return resolvePartial(_Method, _Method.ReturnType, _ArgTypes);
		}

		private void resolveGetMember(Context ctx, GetMemberNode node)
		{
			_InvocationSource = node.Expression;
			var type = _InvocationSource != null
				? _InvocationSource.Resolve(ctx)
				: ctx.ResolveType(node.StaticType);

			checkTypeInSafeMode(ctx, type);

			if (node.TypeHints != null && node.TypeHints.Count > 0)
				_TypeHints = node.TypeHints.Select(x => ctx.ResolveType(x, true)).ToArray();

			try
			{
				// resolve a normal method
				try
				{
					_Method = ctx.ResolveMethod(
						type,
						node.MemberName,
						_ArgTypes,
						_TypeHints,
						(idx, types) => ctx.ResolveLambda(Arguments[idx] as LambdaNode, types)
					);

					if (_Method.IsStatic)
						_InvocationSource = null;

					return;
				}
				catch (KeyNotFoundException)
				{
					if (_InvocationSource == null)
						throw;
				}

				// resolve a callable field
				try
				{
					ctx.ResolveField(type, node.MemberName);
					resolveExpression(ctx, node);
					return;
				}
				catch (KeyNotFoundException) { }

				// resolve a callable property
				try
				{
					ctx.ResolveProperty(type, node.MemberName);
					resolveExpression(ctx, node);
					return;
				}
				catch (KeyNotFoundException) { }

				Arguments = (Arguments[0] is UnitNode)
					? new List<NodeBase> {_InvocationSource}
					: new[] {_InvocationSource}.Union(Arguments).ToList();

				var oldArgTypes = _ArgTypes;
				_ArgTypes = Arguments.Select(a => a.Resolve(ctx)).ToArray();
				_InvocationSource = null;

				try
				{
					// resolve a local function that is implicitly used as an extension method
					_Method = ctx.ResolveMethod(
						ctx.MainType.TypeInfo,
						node.MemberName,
						_ArgTypes,
						resolver: (idx, types) => ctx.ResolveLambda(Arguments[idx] as LambdaNode, types)
					);

					return;
				}
				catch (KeyNotFoundException) { }

				// resolve a declared extension method
				// most time-consuming operation, therefore is last checked
				try
				{
					if(!ctx.Options.AllowExtensionMethods)
						throw new KeyNotFoundException();

					_Method = ctx.ResolveExtensionMethod(
						type,
						node.MemberName,
						oldArgTypes,
						_TypeHints,
						(idx, types) => ctx.ResolveLambda(Arguments[idx] as LambdaNode, types)
					);
				}
				catch (KeyNotFoundException)
				{
					var msg = node.StaticType != null
						? CompilerMessages.TypeStaticMethodNotFound
						: CompilerMessages.TypeMethodNotFound;

					error(msg, type, node.MemberName);
				}
			}
			catch (AmbiguousMatchException)
			{
				error(CompilerMessages.TypeMethodInvocationAmbiguous, type, node.MemberName);
			}
		}

		private void resolveGetIdentifier(Context ctx, GetIdentifierNode node)
		{
			var nameInfo = ctx.Scope.FindLocal(node.Identifier);
			if (nameInfo != null)
			{
				resolveExpression(ctx, node);
				return;
			}

			try
			{
				_Method = ctx.ResolveMethod(
					ctx.MainType.TypeInfo,
					node.Identifier,
					_ArgTypes,
					resolver: (idx, types) => ctx.ResolveLambda(Arguments[idx] as LambdaNode, types)
				);

				if (_Method == null)
					throw new KeyNotFoundException();

				if(_ArgTypes.Length == 0 && node.Identifier.IsAnyOf(EntityNames.RunMethodName, EntityNames.EntryPointMethodName))
					error(CompilerMessages.ReservedFunctionInvocation, node.Identifier);
			}
			catch (KeyNotFoundException)
			{
				error(CompilerMessages.FunctionNotFound, node.Identifier);
			}
			catch (AmbiguousMatchException)
			{
				error(CompilerMessages.FunctionInvocationAmbiguous, node.Identifier);
			}
		}

		private void resolveExpression(Context ctx, NodeBase node)
		{
			var exprType = node.Resolve(ctx);
			if (!exprType.IsCallableType())
				error(CompilerMessages.TypeNotCallable, exprType);

			try
			{
				// argtypes are required for partial application
				_Method = ctx.ResolveMethod(exprType, "Invoke", _ArgTypes);
			}
			catch (KeyNotFoundException)
			{
				// delegate argument types are mismatched:
				// infer whatever method there is and detect actual error
				_Method = ctx.ResolveMethod(exprType, "Invoke");

				var argTypes = _Method.ArgumentTypes;
				if (argTypes.Length != _ArgTypes.Length)
					error(CompilerMessages.DelegateArgumentsCountMismatch, exprType, argTypes.Length, _ArgTypes.Length);

				for (var idx = 0; idx < argTypes.Length; idx++)
				{
					var fromType = _ArgTypes[idx];
					var toType = argTypes[idx];
					if (!toType.IsExtendablyAssignableFrom(fromType))
						error(Arguments[idx], CompilerMessages.ArgumentTypeMismatch, fromType, toType);
				}
			}

			_InvocationSource = node;
		}

		#endregion

		protected override IEnumerable<NodeChild> getChildren()
		{
			var canExpandExpr = !(Expression is GetIdentifierNode || Expression is GetMemberNode);
			if(canExpandExpr)
				yield return new NodeChild(Expression, x => Expression = x);

			foreach (var curr in base.getChildren())
				yield return curr;
		}

		public override void ProcessClosures(Context ctx)
		{
			if(Expression is GetIdentifierNode || Expression is GetMemberNode)
				Expression.ProcessClosures(ctx);

			base.ProcessClosures(ctx);
		}

		#region Compile

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			if (_InvocationSource != null)
			{
				var type = _InvocationSource.Resolve(ctx);

				if (type.IsValueType)
				{
					if (_InvocationSource is IPointerProvider)
					{
						(_InvocationSource as IPointerProvider).PointerRequired = true;
						_InvocationSource.Emit(ctx, true);
					}
					else
					{
						var tmpVar = ctx.Scope.DeclareImplicit(ctx, type, true);
						gen.EmitLoadLocal(tmpVar.LocalBuilder, true);

						_InvocationSource.Emit(ctx, true);
						gen.EmitSaveObject(type);

						gen.EmitLoadLocal(tmpVar.LocalBuilder, true);
					}
				}
				else
				{
					_InvocationSource.Emit(ctx, true);
				}
			}

			if (_ArgTypes.Length > 0)
			{
				var destTypes = _Method.ArgumentTypes;
				for (var idx = 0; idx < Arguments.Count; idx++)
				{
					var arg = Arguments[idx];
					var argRef = arg is IPointerProvider && (arg as IPointerProvider).RefArgumentRequired;
					var targetRef = destTypes[idx].IsByRef;

					if (argRef != targetRef)
					{
						if (argRef)
							error(arg, CompilerMessages.ReferenceArgUnexpected);
						else
							error(arg, CompilerMessages.ReferenceArgExpected, idx + 1, destTypes[idx].GetElementType());
					}

					var expr = argRef ? Arguments[idx] : Expr.Cast(Arguments[idx], destTypes[idx]);
					expr.Emit(ctx, true);
				}
			}

			var isVirt = _InvocationSource != null && _InvocationSource.Resolve(ctx).IsClass;
			gen.EmitCall(_Method.MethodInfo, isVirt);
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

		protected override InvocationNodeBase recreateSelfWithArgs(IEnumerable<NodeBase> newArgs)
		{
			return new InvocationNode { Expression = Expression, Arguments = newArgs.ToList() };
		}

		public override string ToString()
		{
			return string.Format("invoke({0}, args: {1})", Expression, string.Join(",", Arguments));
		}
	}
}
