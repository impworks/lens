using System;
using System.Linq;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.Translations;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A node that represents the lambda function.
	/// </summary>
	public class LambdaNode : FunctionNodeBase
	{
		/// <summary>
		/// The pointer to the method entity defined for this lambda.
		/// </summary>
		private MethodEntity _Method;

		public override void ProcessClosures(Context ctx)
		{
			_Method = ctx.CurrentScope.CreateClosureMethod(ctx, Arguments);
			_Method.Body = Body;

			var methodBackup = ctx.CurrentMethod;
			ctx.CurrentMethod = _Method;

			var scope = _Method.Scope;
			scope.InitializeScope(ctx);
			base.ProcessClosures(ctx);
			
			// get evaluated return type
			var retType = Body.GetExpressionType(ctx);
			if(retType == typeof(NullType))
				Error(CompilerMessages.LambdaReturnTypeUnknown);

			_Method.ReturnType = retType.IsVoid() ? typeof(void) : retType;
			_Method.PrepareSelf();

			scope.FinalizeScope(ctx);

			ctx.CurrentMethod = methodBackup;
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var retType = Body.GetExpressionType(ctx);
			var argTypes = Arguments.Select(a => a.Type ?? ctx.ResolveType(a.TypeSignature.Signature)).ToArray();
			return retType == typeof (Unit) || retType == typeof(void)
				? FunctionalHelper.CreateActionType(argTypes)
				: FunctionalHelper.CreateFuncType(retType, argTypes);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			// find constructor
			var retType = Body.GetExpressionType(ctx);
			var type = retType.IsNotVoid()
				? FunctionalHelper.CreateFuncType(retType, _Method.ArgumentTypes)
				: FunctionalHelper.CreateActionType(_Method.ArgumentTypes);
			var ctor = ctx.ResolveConstructor(type, new[] {typeof (object), typeof (IntPtr)});

			var closureInstance = ctx.CurrentScope.ClosureVariable;
			gen.EmitLoadLocal(closureInstance);
			gen.EmitLoadFunctionPointer(_Method.MethodBuilder);
			gen.EmitCreateObject(ctor.ConstructorInfo);
		}
	}
}
