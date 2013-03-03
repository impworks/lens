using System;
using System.Linq;
using System.Reflection;
using Lens.SyntaxTree.Compiler;
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
			var argTypes = Arguments.Select(a => ctx.ResolveType(a.TypeSignature.Signature)).ToArray();
			_Method = ctx.CurrentScope.CreateClosureMethod(ctx, argTypes);
			_Method.Body = Body;

			var methodBackup = ctx.CurrentMethod;
			ctx.CurrentMethod = _Method;

			base.ProcessClosures(ctx);

			ctx.CurrentScope.FinalizeScope(ctx);
			ctx.CurrentMethod = methodBackup;
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var retType = Body.GetExpressionType(ctx);
			var argTypes = Arguments.Select(a => ctx.ResolveType(a.TypeSignature.Signature)).ToArray();
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
			var ctor = type.GetConstructor(new[] {typeof (object), typeof (IntPtr)});

			gen.EmitNull();
			gen.EmitLoadFunctionPointer(_Method.MethodBuilder);
			gen.EmitCreateObject(ctor);
		}
	}
}
