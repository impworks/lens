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
		public LambdaNode()
		{
			Body = new CodeBlockNode(ScopeKind.LambdaRoot);
		}

		/// <summary>
		/// The pointer to the method entity defined for this lambda.
		/// </summary>
		private MethodEntity _Method;

		public override void ProcessClosures(Context ctx)
		{
			// get evaluated return type
			var retType = Body.Resolve(ctx);
			if (retType == typeof(NullType))
				error(CompilerMessages.LambdaReturnTypeUnknown);
			if (retType.IsVoid())
				retType = typeof (void);

			_Method = ctx.Scope.CreateClosureMethod(ctx, Arguments, retType);
			_Method.Body.LoadFrom(Body);

			var outerMethod = ctx.CurrentMethod;
			ctx.CurrentMethod = _Method;

			base.ProcessClosures(ctx);

			ctx.CurrentMethod = outerMethod;
		}

		protected override Type resolve(Context ctx, bool mustReturn)
		{
			var retType = Body.Resolve(ctx);
			var argTypes = Arguments.Select(a => a.Type ?? ctx.ResolveType(a.TypeSignature)).ToArray();
			return FunctionalHelper.CreateDelegateType(retType, argTypes);
		}

		protected override void emitCode(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentMethod.Generator;

			// find constructor
			var type = FunctionalHelper.CreateDelegateType(Body.Resolve(ctx), _Method.ArgumentTypes);
			var ctor = ctx.ResolveConstructor(type, new[] {typeof (object), typeof (IntPtr)});

			var closureInstance = ctx.Scope.ClosureVariable;
			gen.EmitLoadLocal(closureInstance);
			gen.EmitLoadFunctionPointer(_Method.MethodBuilder);
			gen.EmitCreateObject(ctor.ConstructorInfo);
		}
	}
}
