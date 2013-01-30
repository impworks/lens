using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Lens.SyntaxTree.Compiler
{
	internal class ConstructorEntity : MethodEntityBase
	{
		#region Fields

		public ConstructorBuilder ConstructorBuilder { get; private set; }

		#endregion

		#region Methods

		public override void PrepareSelf(Context ctx)
		{
			if (_IsPrepared)
				return;

			var paramTypes = Arguments.Values.Select(fa => ctx.ResolveType(fa.Type.Signature)).ToArray();
			ConstructorBuilder = ContainerType.TypeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, paramTypes);
			_IsPrepared = true;
		}

		#endregion
	}
}
