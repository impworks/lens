using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

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
		public Type ReturnType { get; private set; }

		/// <summary>
		/// Assembly-level method builder.
		/// </summary>
		public MethodBuilder MethodBuilder { get; private set; }

		#endregion

		#region Methods

		/// <summary>
		/// Creates a MethodBuilder for current method entity.
		/// </summary>
		/// <param name="ctx"></param>
		public override void PrepareSelf()
		{
			if (_IsPrepared)
				return;

			var ctx = ContainerType.Context;

			var attrs = MethodAttributes.Public;
			if(IsStatic)
				attrs |= MethodAttributes.Static;
			else if(IsVirtual)
				attrs |= MethodAttributes.Virtual;

			ReturnType = Body.GetExpressionType(ctx);

			if (ArgumentTypes == null)
				ArgumentTypes = Arguments == null
					? new Type[0]
					: Arguments.Values.Select(fa => ctx.ResolveType(fa.Type.Signature)).ToArray();

			MethodBuilder = ContainerType.TypeBuilder.DefineMethod(Name, attrs, ReturnType, ArgumentTypes);

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

		#endregion
	}
}
