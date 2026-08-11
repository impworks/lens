using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.Resolver;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Literals;
using Lens.Translations;

namespace Lens.Compiler.Entities
{
    /// <summary>
    /// An assembly-level method.
    /// </summary>
    internal class MethodEntity : MethodEntityBase
    {
        #region Constructor

        public MethodEntity(TypeEntity type, bool isImported = false) : base(type, isImported)
        {
            var scopeKind = type.Kind == TypeEntityKind.Closure
                ? ScopeKind.LambdaRoot
                : ScopeKind.FunctionRoot;

            Body = new CodeBlockNode(scopeKind);
        }

        #endregion

        #region Fields

        public bool IsVirtual;
        public bool IsPure;
        public bool IsVariadic;

        /// <summary>
        /// The generic parameters of the method, or null if the method is not generic.
        /// </summary>
        public List<GenericParameterEntity> GenericParameters;

        /// <summary>
        /// The number of generic parameters the method declares.
        /// </summary>
        public int GenericParameterCount => GenericParameters?.Count ?? 0;

        /// <summary>
        /// Checks if the method declares generic parameters.
        /// </summary>
        public bool IsGeneric => GenericParameterCount > 0;

        public override bool IsVoid => ReturnType.IsVoid();

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

        private MethodInfo _methodInfo;

        public MethodInfo MethodInfo
        {
            get => IsImported ? _methodInfo : MethodBuilder;
            set => _methodInfo = value;
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

            var attrs = MethodAttributes.Public;
            if (IsStatic)
                attrs |= MethodAttributes.Static;
            if (IsVirtual)
                attrs |= MethodAttributes.Virtual | MethodAttributes.NewSlot;

            if (IsGeneric)
            {
                // the generic parameters are the very types the signature refers to, so they must
                // be defined before the signature is resolved:
                // DefineMethod -> DefineGenericParameters -> constraints -> SetParameters/SetReturnType
                MethodBuilder = ContainerType.TypeBuilder.DefineMethod(Name, attrs);

                var builders = MethodBuilder.DefineGenericParameters(GenericParameters.Select(p => p.Name).ToArray());
                for (var idx = 0; idx < builders.Length; idx++)
                    GenericParameters[idx].Builder = builders[idx];

                ctx.ResolveGenericParameters(GenericParameters);

                ctx.WithGenericScope(GenericParameters, ResolveSignature);

                MethodBuilder.SetParameters(ArgumentTypes);
                MethodBuilder.SetReturnType(ReturnType.IsVoid() ? typeof(void) : ReturnType);
            }
            else
            {
                ResolveSignature();

                MethodBuilder = ContainerType.TypeBuilder.DefineMethod(Name, attrs, ReturnType.IsVoid() ? typeof(void) : ReturnType, ArgumentTypes);
            }

            Generator = MethodBuilder.GetILGenerator(Context.IlStreamSize);

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

        /// <summary>
        /// Resolves the return type and the argument types of the method.
        /// </summary>
        private void ResolveSignature()
        {
            var ctx = ContainerType.Context;

            if (ReturnType == null)
                ReturnType = ReturnTypeSignature == null || string.IsNullOrEmpty(ReturnTypeSignature.FullSignature)
                    ? typeof(UnitType)
                    : ctx.ResolveType(ReturnTypeSignature);

            if (ArgumentTypes == null)
                ArgumentTypes = Arguments == null
                    ? new Type[0]
                    : Arguments.Values.Select(fa => fa.GetArgumentType(ctx)).ToArray();
        }

        #endregion

        #region Extension points

        protected override void EmitTrailer(Context ctx)
        {
            var gen = ctx.CurrentMethod.Generator;
            var actualType = Body.Resolve(ctx);

            if (!ReturnType.IsVoid() || !actualType.IsVoid())
            {
                if (!ReturnType.IsExtendablyAssignableFrom(ctx.Resolver, actualType))
                    Context.Error(Body.Last(), CompilerMessages.ReturnTypeMismatch, ReturnType, actualType);
            }

            if (ReturnType == typeof(object) && actualType.IsValueType && !actualType.IsVoid())
                gen.EmitBox(actualType);

            // special hack: if the main method's implicit type is Unit, it should still return null
            if (this == ctx.MainMethod && actualType.IsVoid())
                gen.EmitNull();
        }

        #endregion
    }
}