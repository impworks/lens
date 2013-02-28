using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Lens.SyntaxTree.Compiler
{
	internal class ConstructorEntity : MethodEntityBase
	{
		#region Fields

		/// <summary>
		/// Assembly-level constructor builder.
		/// </summary>
		public ConstructorBuilder ConstructorBuilder { get; private set; }

		#endregion

		#region Methods

		/// <summary>
		/// Creates a ConstructorBuilder for current constructor entity.
		/// </summary>
		public override void PrepareSelf()
		{
			// todo: remove when we support static ctors
			if(IsStatic)
				throw new LensCompilerException("A constructor must not be marked as static!");

			if (_IsPrepared)
				return;

			if (ArgumentTypes == null)
				ArgumentTypes = Arguments == null
					? new Type[0]
					: Arguments.Values.Select(fa => ContainerType.Context.ResolveType(fa.TypeSignature.Signature)).ToArray();

			ConstructorBuilder = ContainerType.TypeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, ArgumentTypes);
			Generator = ConstructorBuilder.GetILGenerator(Context.ILStreamSize);
			_IsPrepared = true;
		}

		protected override void compileCore(Context ctx)
		{
			Body.Compile(ctx, false);
		}

		// call default constructor
		protected override void emitPrelude(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;
			var ctor = typeof (object).GetConstructor(Type.EmptyTypes);

			gen.EmitLoadArgument(0);
			gen.EmitCall(ctor);

			base.emitPrelude(ctx);
		}

		#endregion
	}
}
