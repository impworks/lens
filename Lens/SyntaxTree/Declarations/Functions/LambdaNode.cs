using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.SyntaxTree.ControlFlow;
using Lens.Translations;

namespace Lens.SyntaxTree.Declarations.Functions
{
	/// <summary>
	/// A node that represents the lambda function.
	/// </summary>
	internal class LambdaNode : FunctionNodeBase
	{
		#region Constructor

		public LambdaNode()
		{
			Body = new CodeBlockNode(ScopeKind.LambdaRoot);
		}

		#endregion

		#region Fields

		private MethodEntity _Method;

		private Type _InferredReturnType;
		private Type _InferredDelegateType;

		public bool MustInferArgTypes { get; private set; }

		#endregion

		#region Resolve

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var argTypes = new List<Type>();
			foreach (var curr in Arguments)
			{
				if (curr.IsVariadic)
					error(CompilerMessages.VariadicArgumentLambda);

				var type = curr.GetArgumentType(ctx);
				argTypes.Add(type);

				if (type == typeof (UnspecifiedType))
					MustInferArgTypes = true;
			}

			if (MustInferArgTypes)
				return FunctionalHelper.CreateLambdaType(argTypes.ToArray());

			Body.Scope.RegisterArguments(ctx, false, Arguments);

			var retType = Body.Resolve(ctx);

			if (_InferredDelegateType != null)
			{
				if(!_InferredReturnType.IsExtendablyAssignableFrom(retType))
					error(CompilerMessages.LambdaReturnTypeMismatch, _InferredDelegateType.Name, retType.Name, _InferredReturnType.Name);

				return _InferredDelegateType;
			}

			return FunctionalHelper.CreateDelegateType(retType, argTypes.ToArray());
		}

		#endregion

		#region Process closures

		public override void ProcessClosures(Context ctx)
		{
			if (MustInferArgTypes)
			{
				var name = Arguments.First(a => a.Type == typeof (UnspecifiedType)).Name;
				error(CompilerMessages.LambdaArgTypeUnknown, name);
			}

			// get evaluated return type
			var retType = _InferredReturnType ?? Body.Resolve(ctx);
			if (retType == typeof(NullType))
				error(CompilerMessages.LambdaReturnTypeUnknown);
			if (retType.IsVoid())
				retType = typeof (void);

			_Method = ctx.Scope.CreateClosureMethod(ctx, Arguments, retType);
			_Method.Body = Body;

			var outerMethod = ctx.CurrentMethod;
			ctx.CurrentMethod = _Method;

			_Method.Body.ProcessClosures(ctx);

			ctx.CurrentMethod = outerMethod;
		}

		#endregion

		#region Emit

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			// find constructor
			var type = _InferredDelegateType ?? FunctionalHelper.CreateDelegateType(Body.Resolve(ctx), _Method.ArgumentTypes);
			var ctor = ctx.ResolveConstructor(type, new[] {typeof (object), typeof (IntPtr)});

			var closureInstance = ctx.Scope.ActiveClosure.ClosureVariable;
			gen.EmitLoadLocal(closureInstance);
			gen.EmitLoadFunctionPointer(_Method.MethodBuilder);
			gen.EmitCreateObject(ctor.ConstructorInfo);
		}

		#endregion

		#region Argument type detection

		/// <summary>
		/// Sets correct types for arguments which are inferred from usage (invocation, assignment, type casting).
		/// </summary>
		public void SetInferredArgumentTypes(Type[] argTypes)
		{
			for (var idx = 0; idx < argTypes.Length; idx++)
			{
				var inferred = argTypes[idx];
				if (inferred == typeof(UnspecifiedType))
					error(CompilerMessages.LambdaArgTypeUnknown, Arguments[idx].Name);

#if DEBUG
				var specified = Arguments[idx].Type;
				if (specified != typeof(UnspecifiedType) && specified != inferred)
					throw new InvalidOperationException(string.Format("Argument type differs: specified '{0}', inferred '{1}'!", specified, inferred));
#endif

				Arguments[idx].Type = inferred;
			}

			MustInferArgTypes = false;
			_CachedExpressionType = null;
		}

		/// <summary>
		/// Interprets the lambda as a particular delegate with given arg & return types.
		/// </summary>
		public void SetInferredReturnType(Type type)
		{
			_InferredReturnType = type;
		}

		#endregion

		public override string ToString()
		{
			var arglist = Arguments.Select(x => string.Format("{0}:{1}", x.Name, x.Type != null ? x.Type.Name : x.TypeSignature));
			return string.Format("lambda({0})", string.Join(", ", arglist));
		}
	}
}
