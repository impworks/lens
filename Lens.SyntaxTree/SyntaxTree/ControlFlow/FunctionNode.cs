using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.Compiler;

namespace Lens.SyntaxTree.SyntaxTree.ControlFlow
{
	/// <summary>
	/// A function that has a name.
	/// </summary>
	public class FunctionNode : FunctionNodeBase
	{
		/// <summary>
		/// Function name.
		/// </summary>
		public string Name { get; set; }

		/// <summary>
		/// Define function-related assembly entities.
		/// </summary>
		public override void PrepareSelf(Context ctx)
		{
			MethodBuilder = defineMethod(ctx);
			defineParams();
		}

		public override void Compile(Context ctx, bool mustReturn)
		{
			throw new NotImplementedException();
		}

		#region Helpers

		/// <summary>
		/// Define a method in the assembly.
		/// </summary>
		private MethodBuilder defineMethod(Context ctx)
		{
			var retType = Body.GetExpressionType(ctx);
			var args = Arguments.Select(x => ctx.ResolveType(x.Value.Type.Signature)).ToArray();

			return ctx.MainType.DefineMethod(
				Name,
				MethodAttributes.Private | MethodAttributes.Static,
				CallingConventions.Standard,
				retType,
				args
			);
		}

		/// <summary>
		/// Define parameters in the assembly.
		/// </summary>
		private void defineParams()
		{
			var argId = 1;
			foreach (var argInfo in Arguments)
			{
				// todo: ref parameters?
				var arg = argInfo.Value;
				var argKind = arg.Modifier == ArgumentModifier.Out ? ParameterAttributes.Out : ParameterAttributes.In;
				arg.ParameterBuilder = MethodBuilder.DefineParameter(argId, argKind, arg.Name);
				argId++;
			}
		}

		#endregion

		#region Equality members

		protected bool Equals(FunctionNode other)
		{
			return base.Equals(other) && string.Equals(Name, other.Name);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(null, obj)) return false;
			if (ReferenceEquals(this, obj)) return true;
			if (obj.GetType() != this.GetType()) return false;
			return Equals((FunctionNode)obj);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return (base.GetHashCode() * 397) ^ (Name != null ? Name.GetHashCode() : 0);
			}
		}

		#endregion
	}
}
