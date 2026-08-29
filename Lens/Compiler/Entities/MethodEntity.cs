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

        /// <summary>
        /// The method replaces an inherited virtual method rather than introducing a new one.
        ///
        /// The distinction is the whole difference between overriding and shadowing: a virtual
        /// method marked NewSlot takes a fresh vtable slot and leaves the inherited one alone, so a
        /// caller reaching the object through its base type never sees it.
        /// </summary>
        public bool IsOverride;

        public bool IsPure;
        public bool IsVariadic;

        /// <summary>
        /// The interface method this one implements under a name of its own.
        ///
        /// Only needed where implicit matching cannot work: IEnumerable and IEnumerable&lt;T&gt;
        /// both declare a GetEnumerator that takes nothing, and they differ only in what they
        /// return, so at most one of the two can be implemented by a method actually called
        /// GetEnumerator.
        /// </summary>
        public MethodInfo ExplicitOverride;

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
        public TypeEntry ReturnType;

        /// <summary>
        /// Whether the signature has already been resolved. The analysis half runs at most once,
        /// however many times preparation is asked for.
        /// </summary>
        private bool _isResolved;

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
        /// Resolves the signature of the method and the constraint model of its generic parameters.
        ///
        /// A generic method used to have to wait for its parameter builders, because a composite
        /// signature like Option&lt;T&gt; was resolved into a constructed CLR type. Now that a
        /// signature resolves into an entry, nothing here needs an assembly.
        /// </summary>
        public override void ResolveSelf()
        {
            if (IsImported || _isResolved)
                return;

            ResolveSelfCore();
        }

        /// <summary>
        /// Resolves the signature, whatever phase asked for it.
        /// </summary>
        private void ResolveSelfCore()
        {
            if (_isResolved)
                return;

            _isResolved = true;

            var ctx = ContainerType.Context;

            if (IsGeneric)
            {
                ctx.RegisterGenericParameters(GenericParameters);
                ctx.WithGenericScope(GenericParameters, ResolveSignature);
            }
            else
            {
                ResolveSignature();
            }

            // an empty script is allowed and it's return is null
            if (this == ctx.MainMethod && Body.Statements.Count == 0)
                Body.Statements.Add(new UnitNode());
        }

        /// <summary>
        /// Creates a MethodBuilder for current method entity.
        /// </summary>
        public override void EmitSelf()
        {
            if (MethodBuilder != null || IsImported)
                return;

            var ctx = ContainerType.Context;

            var attrs = MethodAttributes.Public;
            if (IsStatic)
                attrs |= MethodAttributes.Static;
            if (IsVirtual)
            {
                attrs |= MethodAttributes.Virtual;

                // an override has to reuse the slot it inherits, so NewSlot is exactly wrong for it;
                // a method that implements an interface does want a slot of its own
                if (!IsOverride)
                    attrs |= MethodAttributes.NewSlot;
            }

            if (IsGeneric)
            {
                // the generic parameters are the very types a composite signature refers to, so
                // they must be defined before the signature is resolved:
                // DefineMethod -> DefineGenericParameters -> constraints -> SetParameters/SetReturnType
                MethodBuilder = ContainerType.TypeBuilder.DefineMethod(Name, attrs);

                var builders = MethodBuilder.DefineGenericParameters(GenericParameters.Select(p => p.Name).ToArray());
                for (var idx = 0; idx < builders.Length; idx++)
                    GenericParameters[idx].Builder = builders[idx];

                // the constraint model is registered and applied before the signature is resolved,
                // exactly as it always was: a signature that instantiates a constrained generic
                // type over one of these parameters is checked against the model
                ctx.RegisterGenericParameters(GenericParameters);
                ctx.EmitGenericParameters(GenericParameters);

                ResolveSelfCore();

                MethodBuilder.SetParameters(TypeEntry.Materialize(ArgumentTypes));
                MethodBuilder.SetReturnType(ReturnType.IsVoid() ? typeof(void) : ReturnType.Materialize());
            }
            else
            {
                ResolveSelfCore();

                MethodBuilder = ContainerType.TypeBuilder.DefineMethod(Name, attrs, ReturnType.IsVoid() ? typeof(void) : ReturnType.Materialize(), TypeEntry.Materialize(ArgumentTypes));
            }

            if (ExplicitOverride != null)
                ContainerType.TypeBuilder.DefineMethodOverride(MethodBuilder, ExplicitOverride);

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
        }

        /// <summary>
        /// Resolves the return type and the argument types of the method.
        /// </summary>
        private void ResolveSignature()
        {
            var ctx = ContainerType.Context;

            if (ReturnType == null)
                ReturnType = ReturnTypeSignature == null || string.IsNullOrEmpty(ReturnTypeSignature.FullSignature)
                    ? TypeEntryCache.Of<UnitType>()
                    : ctx.ResolveType(ReturnTypeSignature);

            if (ArgumentTypes == null)
                ArgumentTypes = Arguments == null
                    ? new TypeEntry[0]
                    : Arguments.Values.Select(fa => fa.GetArgumentType(ctx)).ToArray();

            // a signature names types the same way the body does, and safe mode has to see them
            // here rather than at the first call: 'fun f:Forbidden' otherwise reports the problem
            // wherever f happens to be used, and 'fun f (x:Forbidden)' not at all
            CheckSignatureInSafeMode(ctx);
        }

        /// <summary>
        /// Applies the safe mode restrictions to the types the signature names.
        /// </summary>
        private void CheckSignatureInSafeMode(Context ctx)
        {
            if (!ctx.IsTypeAllowed(ReturnType))
                Fail(ReturnType);

            foreach (var curr in ArgumentTypes)
                if (!ctx.IsTypeAllowed(curr))
                    Fail(curr);

            void Fail(TypeEntry type)
            {
                var message = string.Format(CompilerMessages.SafeModeIllegalType, type.FullName);

                throw Body == null
                    ? new LensCompilerException(message)
                    : new LensCompilerException(message, Body);
            }
        }

        /// <summary>
        /// Checks that the body actually produces the value the signature promises.
        /// </summary>
        private void CheckReturnType(Context ctx, TypeEntry actualType)
        {
            // the script's root method is declared as returning object, and returns null when the
            // last statement of the script has no value: that is not a mismatch
            if (this == ctx.MainMethod && actualType.IsVoid())
                return;

            if (ReturnType.IsVoid() && actualType.IsVoid())
                return;

            if (!ReturnType.IsExtendablyAssignableFrom(ctx.Resolver, actualType))
                Context.Error(Body.Last(), CompilerMessages.ReturnTypeMismatch, ReturnType, actualType);
        }

        #endregion

        #region Extension points

        protected override void CheckBody(Context ctx)
        {
            // the root method is declared as returning object and accepts a body of any type at all,
            // the absence of a value included: there is nothing here for the check to find, and
            // resolving the body as a whole would only anticipate what emission does anyway
            if (this == ctx.MainMethod)
                return;

            // the check belongs to the analysis half rather than to emission: nothing about it needs
            // an assembly, and an editor must report a body that does not return the declared value
            CheckReturnType(ctx, Body.Resolve(ctx));
        }

        protected override void EmitTrailer(Context ctx)
        {
            var gen = ctx.CurrentMethod.Generator;
            var actualType = Body.Resolve(ctx);

            // a closure method never goes through the analysis half, so it is checked here
            CheckReturnType(ctx, actualType);

            if (ReturnType.Is<object>() && actualType.IsValueType && !actualType.IsVoid())
                gen.EmitBox(actualType.Materialize());

            // special hack: if the main method's implicit type is Unit, it should still return null
            if (this == ctx.MainMethod && actualType.IsVoid())
                gen.EmitNull();
        }

        #endregion
    }
}