using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.SyntaxTree.ControlFlow;
using Lens.Translations;

namespace Lens.Compiler.Entities
{
    /// <summary>
    /// An assembly-level constructor.
    /// </summary>
    internal class ConstructorEntity : MethodEntityBase
    {
        #region Constructor

        public ConstructorEntity(TypeEntity type) : base(type)
        {
            Body = new CodeBlockNode(ScopeKind.FunctionRoot);
        }

        #endregion

        #region Fields

        public override bool IsVoid => true;

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
            if (IsStatic)
                throw new LensCompilerException(CompilerMessages.ConstructorStatic);

            if (ConstructorBuilder != null || IsImported)
                return;

            var ctx = ContainerType.Context;

            if (ArgumentTypes == null)
                ArgumentTypes = Arguments == null
                    ? new Type[0]
                    : Arguments.Values.Select(fa => fa.GetArgumentType(ctx)).ToArray();

            ConstructorBuilder = ContainerType.TypeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.HasThis, ArgumentTypes);
            Generator = ConstructorBuilder.GetILGenerator(Context.IlStreamSize);
        }

        #endregion

        #region Extension points

        // call default constructor
        protected override void EmitPrelude(Context ctx)
        {
            var gen = ctx.CurrentMethod.Generator;
            var ctor = typeof(object).GetConstructor(Type.EmptyTypes);

            gen.EmitLoadArgument(0);
            gen.EmitCall(ctor);

            base.EmitPrelude(ctx);
        }

        #endregion
    }
}