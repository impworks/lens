using System;
using System.Collections.Generic;
using System.Reflection;
using Lens.SyntaxTree.Compiler;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.SyntaxTree.Expressions
{
	/// <summary>
	/// A node representing read access to a local variable or a function.
	/// </summary>
	public class GetIdentifierNode : IdentifierNodeBase, IEndLocationTrackingEntity
	{
		public GetIdentifierNode(string identifier = null)
		{
			Identifier = identifier;
		}

		protected override Type resolveExpressionType(Context ctx, bool mustReturn = true)
		{
			var nameInfo = LocalName ?? ctx.CurrentScope.FindName(Identifier);
			if (nameInfo != null)
				return nameInfo.Type;

			try
			{
				var method = ctx.MainType.ResolveMethod(Identifier, Type.EmptyTypes);
				return FunctionalHelper.CreateFuncType(method.ReturnType);
			}
			catch (KeyNotFoundException)
			{
				Error("No local variable or global parameterless function named '{0}' was found.", Identifier);
			}

			return typeof (Unit);
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			var gen = ctx.CurrentILGenerator;

			// load local variable
			var nameInfo = LocalName ?? ctx.CurrentScope.FindName(Identifier);
			if (nameInfo != null)
			{
				if (nameInfo.IsClosured)
					getClosured(ctx, nameInfo);
				else
					getLocal(ctx, nameInfo);

				return;
			}

			// load pointer to global function
			try
			{
				var method = ctx.MainType.ResolveMethod(Identifier, Type.EmptyTypes);
				var funcType = FunctionalHelper.CreateFuncType(method.ReturnType);
				var ctor = funcType.GetConstructor(new[] {typeof (object), typeof (IntPtr)});

				gen.EmitNull();
				gen.EmitLoadFunctionPointer(method);
				gen.EmitCreateObject(ctor);
			}
			catch (KeyNotFoundException)
			{
				Error("No local variable or global parameterless function named '{0}' was found.", Identifier);
			}
		}

		private void getClosured(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;
			gen.EmitLoadLocal(ctx.CurrentScope.ClosureVariable);

			var dist = name.ClosureDistance;
			var scope = ctx.CurrentScope;
			while (dist > 0)
			{
				var rootField = scope.ClosureType.ResolveField(Scope.ParentScopeFieldName);
				gen.EmitLoadField(rootField);

				scope = scope.OuterScope;
				dist--;
			}

			var clsField = scope.ClosureType.ResolveField(name.ClosureFieldName);
			gen.EmitLoadField(clsField);
		}

		private void getLocal(Context ctx, LocalName name)
		{
			var gen = ctx.CurrentILGenerator;
			gen.EmitLoadLocal(name);
		}

		public override string ToString()
		{
			return string.Format("get({0})", Identifier);
		}
	}
}
