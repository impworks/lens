using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.Compiler
{
	internal class MethodEntity : MethodEntityBase
	{
		public MethodEntity()
		{
			Body = new CodeBlockNode();
		}

		#region Fields

		public bool IsStatic { get; set; }

		public bool IsVirtual { get; set; }

		public Type ReturnType { get; set; }

		public MethodBuilder MethodBuilder { get; private set; }

		public CodeBlockNode Body { get; private set; }

		#endregion

		#region Methods

		public override void PrepareSelf(Context ctx)
		{
			var attrs = MethodAttributes.Public;
			if(IsStatic)
				attrs |= MethodAttributes.Static;
			else if(IsVirtual)
				attrs |= MethodAttributes.Virtual;

			var paramTypes = Arguments.Values.Select(fa => ctx.ResolveType(fa.Type.Signature)).ToArray();
			MethodBuilder = ContainerType.TypeBuilder.DefineMethod(Name, attrs, ReturnType, paramTypes);

			var idx = 1;
			foreach (var param in Arguments.Values)
			{
				var pa = param.Modifier == ArgumentModifier.In ? ParameterAttributes.In : ParameterAttributes.Out;
				param.ParameterBuilder = MethodBuilder.DefineParameter(idx, pa, param.Name);
				idx++;
			}
		}

		#endregion
	}
}
