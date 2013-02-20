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
			if (_IsPrepared)
				return;

			if (ArgumentTypes == null)
				ArgumentTypes = Arguments == null
					? new Type[0]
					: Arguments.Values.Select(fa => ContainerType.Context.ResolveType(fa.Type.Signature)).ToArray();

			ConstructorBuilder = ContainerType.TypeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, ArgumentTypes);
			Generator = ConstructorBuilder.GetILGenerator(Context.ILStreamSize);
			_IsPrepared = true;
		}

		protected override void compileCore(Context ctx)
		{
			Body.Compile(ctx, false);
		}

		#endregion
	}
}
