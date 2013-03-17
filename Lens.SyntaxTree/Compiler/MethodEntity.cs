using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.SyntaxTree.Literals;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree.Compiler
{
	internal class MethodEntity : MethodEntityBase
	{
		public MethodEntity(bool isImported = false) : base(isImported)
		{ }

		#region Fields

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

		private MethodInfo m_MethodInfo;
		public MethodInfo MethodInfo
		{
			get { return IsImported ? m_MethodInfo : MethodBuilder; }
			set
			{
				if (!IsImported)
					throw new LensCompilerException(string.Format("Method '{0}' is not imported!", Name));

				m_MethodInfo = value;
			}
		}

		#endregion

		#region Methods

		/// <summary>
		/// Creates a MethodBuilder for current method entity.
		/// </summary>
		public override void PrepareSelf()
		{
			if (MethodBuilder != null || IsImported)
				return;

			var ctx = ContainerType.Context;

			if (ctx.MethodResolutionStack.Contains(this))
				throw new LensCompilerException(
					string.Format("The return type of method '{0}' cannot be inferred due to recursion! Please use type casting to specify the return type.", Name)
				);

			ctx.MethodResolutionStack.Push(this);

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
					: Arguments.Values.Select(fa => fa.GetArgumentType(ctx)).ToArray();

			MethodBuilder = ContainerType.TypeBuilder.DefineMethod(Name, attrs, ReturnType, ArgumentTypes);
			Generator = MethodBuilder.GetILGenerator(Context.ILStreamSize);

			if (Arguments != null)
			{
				var idx = 1;
				foreach (var param in Arguments.Values)
				{
					param.ParameterBuilder = MethodBuilder.DefineParameter(idx, ParameterAttributes.None, param.Name);
					idx++;
				}
			}

			// an empty script is allowed and it's return is null
			if (this == ctx.MainMethod && Body.Statements.Count == 0)
				Body.Statements.Add(new UnitNode());

			ctx.MethodResolutionStack.Pop();
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

			if (ReturnType == typeof(object) && actualType.IsValueType && actualType.IsNotVoid())
				gen.EmitBox(actualType);

			// special hack: if the main method's implicit type is Unit, it should still return null
			if(this == ctx.MainMethod && actualType.IsVoid())
				gen.EmitNull();
		}

		#endregion
	}
}
