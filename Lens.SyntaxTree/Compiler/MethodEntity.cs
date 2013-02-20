using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.Compiler
{
	internal class MethodEntity : MethodEntityBase
	{
		#region Fields

		/// <summary>
		/// Checks if the method belongs to the type, not its instances.
		/// </summary>
		public bool IsStatic;

		/// <summary>
		/// Checks if the method can be overridden in derived types or is overriding a parent method itself.
		/// </summary>
		public bool IsVirtual;

		/// <summary>
		/// The return type of the method.
		/// </summary>
		public Type ReturnType;

		/// <summary>
		/// Assembly-level method builder.
		/// </summary>
		public MethodBuilder MethodBuilder { get; private set; }

		#endregion

		#region Methods

		/// <summary>
		/// Creates a MethodBuilder for current method entity.
		/// </summary>
		public override void PrepareSelf()
		{
			if (_IsPrepared)
				return;

			var ctx = ContainerType.Context;

			var attrs = MethodAttributes.Public;
			if(IsStatic)
				attrs |= MethodAttributes.Static;
			if(IsVirtual)
				attrs |= MethodAttributes.Virtual | MethodAttributes.NewSlot;

			if(ReturnType == null)
				ReturnType = Body.GetExpressionType(ctx);

			if (ArgumentTypes == null)
				ArgumentTypes = Arguments == null
					? new Type[0]
					: Arguments.Values.Select(fa => ctx.ResolveType(fa.Type.Signature)).ToArray();

			MethodBuilder = ContainerType.TypeBuilder.DefineMethod(Name, attrs, ReturnType, ArgumentTypes);
			Generator = MethodBuilder.GetILGenerator(Context.ILStreamSize);

			if (Arguments != null)
			{
				var idx = 1;
				foreach (var param in Arguments.Values)
				{
					var pa = param.Modifier == ArgumentModifier.In ? ParameterAttributes.In : ParameterAttributes.Out;
					param.ParameterBuilder = MethodBuilder.DefineParameter(idx, pa, param.Name);
					idx++;
				}
			}

			_IsPrepared = true;
		}

		protected override void compileCore(Context ctx)
		{
			Body.Compile(ctx, ReturnType.IsNotVoid());

			emitTrailer();
		}

		protected void emitTrailer()
		{
			var ctx = ContainerType.Context;
			var gen = ctx.CurrentILGenerator;
			var actualType = Body.GetExpressionType(ctx);

			if (ReturnType == typeof(object) && actualType.IsValueType && actualType != typeof(void))
				gen.EmitBox(actualType);

			// special hack: if the main method's implicit type is Unit, it should still return null
			if(this == ctx.MainMethod && (actualType == typeof(Unit) || actualType == typeof(void)))
				gen.EmitNull();
		}

		#endregion
	}
}
