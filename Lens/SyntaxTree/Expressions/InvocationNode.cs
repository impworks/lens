using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Compiler;
using Lens.Compiler.Entities;
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

		private Type[] _ArgTypes;
		private Type[] _TypeHints;

		#region Resolve
		
		protected override Type resolve(Context ctx, bool mustReturn = true)
		{
			var isParameterless = Arguments.Count == 1 && Arguments[0].Resolve(ctx) == typeof(Unit);

			_ArgTypes = isParameterless
				? Type.EmptyTypes
				: Arguments.Select(a => a.Resolve(ctx)).ToArray();

			if (Expression is GetMemberNode)
				resolveGetMember(ctx, Expression as GetMemberNode);
			else if (Expression is GetIdentifierNode)
				resolveGetIdentifier(ctx, Expression as GetIdentifierNode);
			else
				resolveExpression(ctx, Expression);

			return _Method.ReturnType;
		}

		public override NodeBase Expand(Context ctx, bool mustReturn)
		{
			if (_Method.IsPartiallyApplied)
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
						var argName = string.Format("<pa_{0}>", ctx.Unique.AnonymousArgName);
						argDefs.Add(Expr.Arg(argName, _Method.ArgumentTypes[idx].FullName));
						argExprs.Add(Expr.Get(argName));
					}
					else
					{
						argExprs.Add(Arguments[idx]);
					}
				}

				return Expr.Lambda(argDefs, Expr.Invoke(Expression, argExprs.ToArray()));
			}

			return base.Expand(ctx, mustReturn);
		}

		private void resolveGetMember(Context ctx, GetMemberNode node)
		{
			_InvocationSource = node.Expression;
			var type = _InvocationSource != null
				? _InvocationSource.Resolve(ctx)
				: ctx.ResolveType(node.StaticType);

			checkTypeInSafeMode(ctx, type);

			if (node.TypeHints.Any())
				_TypeHints = node.TypeHints.Select(x => ctx.ResolveType(x, true)).ToArray();

			try
			{
				// resolve a normal method
				try
				{
					_Method = ctx.ResolveMethod(type, node.MemberName, _ArgTypes, _TypeHints);

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

				try
				{
					// resolve a local function that is implicitly used as an extension method
					var movedArgs = new List<NodeBase> { _InvocationSource };
					if(!(Arguments[0] is UnitNode))
						movedArgs.AddRange(Arguments);

					var argTypes = movedArgs.Select(a => a.Resolve(ctx)).ToArray();

					_Method = ctx.ResolveMethod(ctx.MainType.TypeInfo, node.MemberName, argTypes);

					// if no exception has occured, move invocation source to argument list permanently
					Arguments = movedArgs;
					_ArgTypes = argTypes;
					_InvocationSource = null;

					return;
				}
				catch (KeyNotFoundException) { }

				// resolve a declared extension method
				// most time-consuming operation, therefore is last checked
				try
				{
					if(ctx.Options.AllowExtensionMethods)
						_Method = ctx.ResolveExtensionMethod(type, node.MemberName, _ArgTypes, _TypeHints);
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
			var nameInfo = ctx.Scope.FindName(node.Identifier);
			if (nameInfo != null)
			{
				resolveExpression(ctx, node);
				return;
			}

			try
			{
				_Method = ctx.ResolveMethod(ctx.MainType.TypeInfo, node.Identifier, _ArgTypes);
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

			_InvocationSource = node;
		}

		#endregion

		public override IEnumerable<NodeChild> GetChildren()
		{
			var canExpandExpr = !(Expression is GetIdentifierNode || Expression is GetMemberNode);
			if(canExpandExpr)
				yield return new NodeChild(Expression, x => Expression = x);

			for (var idx = 0; idx < Arguments.Count; idx++)
			{
				var id = idx;
				yield return new NodeChild(Arguments[id], x => Arguments[id] = x);
			}
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
						var tmpVar = ctx.Scope.DeclareImplicitName(ctx, type, true);
						gen.EmitLoadLocal(tmpVar, true);

						_InvocationSource.Emit(ctx, true);
						gen.EmitSaveObject(type);

						gen.EmitLoadLocal(tmpVar, true);
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

			protected
			bool Equals(InvocationNode other)
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
