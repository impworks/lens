using System;
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
		/// <summary>
		/// The pointer to the method entity defined for this lambda.
		/// </summary>
		private MethodEntity _Method;

		public override void ProcessClosures(Context ctx)
		{
			_Method = ctx.CurrentScopeFrame.CreateClosureMethod(ctx, Arguments);
			_Method.Body = Body;

			var outerMethod = ctx.CurrentMethod;
			var outerFrame = ctx.CurrentScopeFrame;

			ctx.CurrentMethod = _Method;
			ctx.CurrentScopeFrame = _Method.Scope.RootFrame;

			var scope = _Method.Scope;
			scope.InitializeScope(ctx, outerFrame);
			base.ProcessClosures(ctx);
			
			// get evaluated return type
			var retType = Body.Resolve(ctx);
			if(retType == typeof(NullType))
				error(CompilerMessages.LambdaReturnTypeUnknown);

			_Method.ReturnType = retType.IsVoid() ? typeof(void) : retType;
			_Method.PrepareSelf();

			scope.FinalizeScope(ctx);

			ctx.CurrentMethod = outerMethod;
			ctx.CurrentScopeFrame = outerFrame;
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var retType = Body.Resolve(ctx);
			var argTypes = Arguments.Select(a => a.Type ?? ctx.ResolveType(a.TypeSignature)).ToArray();
			return FunctionalHelper.CreateDelegateType(retType, argTypes);
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			// find constructor
			var type = FunctionalHelper.CreateDelegateType(Body.Resolve(ctx), _Method.ArgumentTypes);
			var ctor = ctx.ResolveConstructor(type, new[] {typeof (object), typeof (IntPtr)});

			var closureInstance = ctx.CurrentScope.ClosureVariable;
			gen.EmitLoadLocal(closureInstance);
			gen.EmitLoadFunctionPointer(_Method.MethodBuilder);
			gen.EmitCreateObject(ctor.ConstructorInfo);
		}
	}
}
