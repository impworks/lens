using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.Literals;
using Lens.Translations;
using Lens.Utils;

namespace Lens.Compiler.Entities
{
	internal class MethodEntity : MethodEntityBase
	{
		public MethodEntity(bool isImported = false) : base(isImported)
		{ }

		#region Fields

		public bool IsVirtual;

		/// <summary>
		/// The signature of method's return type.
		/// </summary>
		public TypeSignature ReturnTypeSignature;

		/// <summary>
		/// Compiled return type.
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
			set { m_MethodInfo = value; }
		}

		public bool IsPure;

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

			var attrs = MethodAttributes.Public;
			if(IsStatic)
				attrs |= MethodAttributes.Static;
			if(IsVirtual)
				attrs |= MethodAttributes.Virtual | MethodAttributes.NewSlot;

			if (ReturnType == null)
				ReturnType = ReturnTypeSignature == null || string.IsNullOrEmpty(ReturnTypeSignature.FullSignature)
					? typeof(Unit)
					: ctx.ResolveType(ReturnTypeSignature);

			if (ArgumentTypes == null)
				ArgumentTypes = Arguments == null
					? new Type[0]
					: Arguments.Values.Select(fa => fa.GetArgumentType(ctx)).ToArray();

			MethodBuilder = ContainerType.TypeBuilder.DefineMethod(Name, attrs, ReturnType.IsVoid() ? typeof(void) : ReturnType, ArgumentTypes);
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
		}

		protected override void compileCore(Context ctx)
		{
			Body.Compile(ctx, ReturnType.IsNotVoid());
		}

		protected override void emitTrailer(Context ctx)
		{
			var gen = ctx.CurrentILGenerator;
			var actualType = Body.GetExpressionType(ctx);

			if (ReturnType.IsNotVoid() || actualType.IsNotVoid())
			{
				if (!ReturnType.IsExtendablyAssignableFrom(actualType))
					ctx.Error(Body.Last(), CompilerMessages.ReturnTypeMismatch, ReturnType, actualType);
			}

			if (ReturnType == typeof(object) && actualType.IsValueType && actualType.IsNotVoid())
				gen.EmitBox(actualType);

			// special hack: if the main method's implicit type is Unit, it should still return null
			if(this == ctx.MainMethod && actualType.IsVoid())
				gen.EmitNull();
		}

		#endregion
	}
}
