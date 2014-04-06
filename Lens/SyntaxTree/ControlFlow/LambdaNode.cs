using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.SyntaxTree.Literals;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node that represents the lambda function.
	/// </summary>
	internal class LambdaNode : FunctionNodeBase
	{
		public LambdaNode()
		{
			Body = new CodeBlockNode(ScopeKind.LambdaRoot);
		}

		private MethodEntity _Method;
		public bool MustInferArgTypes { get; private set; }

		#region Overrides

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
				return FunctionalHelper.CreateActionType(argTypes.ToArray());

			Body.Scope.RegisterArguments(ctx, false, Arguments);

			var retType = Body.Resolve(ctx);
			return FunctionalHelper.CreateDelegateType(retType, argTypes.ToArray());
		}

		public override void ProcessClosures(Context ctx)
		{
			if (MustInferArgTypes)
			{
				var name = Arguments.First(a => a.Type == typeof (UnspecifiedType)).Name;
				error(CompilerMessages.LambdaArgTypeUnknown, name);
			}

			// get evaluated return type
			var retType = Body.Resolve(ctx);
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

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			// find constructor
			var type = FunctionalHelper.CreateDelegateType(Body.Resolve(ctx), _Method.ArgumentTypes);
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
		public void SetInferredArgumentTypes(Type[] types)
		{
			for (var idx = 0; idx < types.Length; idx++)
			{
				var inferred = types[idx];
				var specified = Arguments[idx].Type;

				if (inferred == null)
				{
					if(specified == typeof(UnspecifiedType))
						error(CompilerMessages.LambdaArgTypeUnknown, Arguments[idx].Name);
					else
						continue;
				}

#if DEBUG
				if (specified != typeof(UnspecifiedType) && specified != inferred)
					throw new InvalidOperationException(string.Format("Argument type differs: specified '{0}', inferred '{1}'!", specified, inferred));
#endif

				Arguments[idx].Type = inferred;
			}

			MustInferArgTypes = false;
		}

		#endregion

		public override string ToString()
		{
			var arglist = Arguments.Select(x => string.Format("{0}:{1}", x.Name, x.Type != null ? x.Type.Name : x.TypeSignature));
			return string.Format("lambda({0})", string.Join(", ", arglist));
		}
	}
}
