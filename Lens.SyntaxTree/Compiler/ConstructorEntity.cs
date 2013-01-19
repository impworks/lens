using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.Compiler
{
	internal class ConstructorEntity : TypeContentsBase
	{
		#region Fields

		public Dictionary<string, FunctionArgument> Arguments { get; set; }

		public ConstructorBuilder ConstructorBuilder { get; private set; }

		#endregion

		#region Methods

		public override void PrepareSelf(Context ctx)
		{
			var paramTypes = Arguments.Values.Select(fa => ctx.ResolveType(fa.Type.Signature)).ToArray();
			ConstructorBuilder = ContainerType.TypeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, paramTypes);
		}

		#endregion
	}
}
