using System;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Compiler.Entities;
using Lens.Resolver;
using Lens.SyntaxTree.ControlFlow;
using Lens.Translations;

namespace Lens.SyntaxTree.Declarations.Functions
{
    /// <summary>
    /// A node that represents the lambda function.
    /// </summary>
    internal class LambdaNode : FunctionNodeBase
    {
        #region Constructor

        public LambdaNode()
        {
            Body = new CodeBlockNode(ScopeKind.LambdaRoot);
        }

        #endregion

        #region Binding

        /// <summary>
        /// What binding learned about this lambda.
        /// </summary>
        private class Binding
        {
            /// <summary>
            /// The method of the closure class that the lambda body was compiled into.
            /// </summary>
            public MethodEntity Method;

            /// <summary>
            /// The return type the surrounding context demands, when it demands one.
            /// </summary>
            public TypeEntry InferredReturnType;
        }

        #endregion

        #region Fields

        /// <summary>
        /// Flag indicating that current lambda has arguments with omitted types and they must be resolved from the context.
        /// </summary>
        public bool MustInferArgTypes { get; private set; }

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            var argTypes = new List<TypeEntry>();
            foreach (var curr in Arguments)
            {
                if (curr.IsVariadic)
                    Error(CompilerMessages.VariadicArgumentLambda);

                var type = curr.GetArgumentType(ctx);
                argTypes.Add(type);

                if (type.Is<UnspecifiedType>())
                    MustInferArgTypes = true;
            }

            if (MustInferArgTypes)
                return TypeEntryCache.Of(FunctionalHelper.CreateLambdaType(TypeEntry.Materialize(argTypes)));

            ctx.ScopeOf(Body).RegisterArguments(ctx, false, Arguments);

            var retType = Body.Resolve(ctx);
            return TypeEntryCache.Of(FunctionalHelper.CreateDelegateType(retType.Materialize(), TypeEntry.Materialize(argTypes)));
        }

        #endregion

        #region Closures

        public override void AnalyzeClosures(Context ctx)
        {
            // validating the signature belongs here rather than in the emission half: the answer
            // does not depend on anything an assembly holds
            ResolveClosureReturnType(ctx);

            Body.AnalyzeClosures(ctx);
        }

        public override void EmitClosureEntities(Context ctx)
        {
            var binding = ctx.BindingOf<Binding>(this);

            binding.Method = ctx.Scope.CreateClosureMethod(ctx, Arguments, ResolveClosureReturnType(ctx));
            binding.Method.Body = Body;

            // the locals of the body belong to the closure method's frame, not to the enclosing one
            var outerMethod = ctx.CurrentMethod;
            ctx.CurrentMethod = binding.Method;

            Body.EmitClosureEntities(ctx);

            ctx.CurrentMethod = outerMethod;
        }

        /// <summary>
        /// Works out the return type of the method the lambda will be compiled into.
        /// </summary>
        private TypeEntry ResolveClosureReturnType(Context ctx)
        {
            if (MustInferArgTypes)
            {
                var name = Arguments.First(a => a.Type == TypeEntryCache.Of<UnspecifiedType>()).Name;
                Error(CompilerMessages.LambdaArgTypeUnknown, name);
            }

            var retType = ctx.BindingOf<Binding>(this).InferredReturnType ?? Body.Resolve(ctx);
            if (retType.Is<NullType>())
                Error(CompilerMessages.LambdaReturnTypeUnknown);

            return retType.IsVoid() ? TypeEntryCache.Of(typeof(void)) : retType;
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            // the delegate type is expressed in the terms of the enclosing method, while the
            // backing method belongs to the closure class and may be generic in its parameters
            var argTypes = Arguments.Select(x => x.GetArgumentType(ctx)).ToArray();
            var type = FunctionalHelper.CreateDelegateType(Body.Resolve(ctx).Materialize(), TypeEntry.Materialize(argTypes));
            var ctor = ctx.ResolveConstructor(TypeEntryCache.Of(type), new[] {TypeEntryCache.Of<object>(), TypeEntryCache.Of<IntPtr>()});

            var closure = ctx.Scope.ActiveClosure;
            var closureMethod = ctx.ResolveMethodGroup(closure.ClosureInstanceType, ctx.BindingOf<Binding>(this).Method.Name).Single();

            // inside a state machine the closure class is the machine, and the instance the
            // delegate must be bound to is the receiver rather than a local
            if (closure.ClosureIsThis)
                gen.EmitLoadArgument(0);
            else
                gen.EmitLoadLocal(closure.ClosureVariable);

            gen.EmitLoadFunctionPointer(closureMethod.MethodInfo);
            gen.EmitCreateObject(ctor.ConstructorInfo);
        }

        #endregion

        #region Argument type detection

        /// <summary>
        /// Sets correct types for arguments which are inferred from usage (invocation, assignment, type casting).
        /// </summary>
        public void SetInferredArgumentTypes(Context ctx, TypeEntry[] argTypes)
        {
            if (Arguments.Count != argTypes.Length)
                Error(CompilerMessages.LambdaArgumentsCountMismatch, argTypes.Length, Arguments.Count);

            for (var idx = 0; idx < argTypes.Length; idx++)
            {
                var inferred = argTypes[idx];
                if (inferred.Is<UnspecifiedType>())
                    Error(CompilerMessages.LambdaArgTypeUnknown, Arguments[idx].Name);

#if DEBUG
                var specified = Arguments[idx].Type;
                if (specified != TypeEntryCache.Of<UnspecifiedType>() && specified != inferred)
                    throw new InvalidOperationException($"Argument type differs: specified '{specified}', inferred '{inferred}'!");
#endif

                Arguments[idx].Type = inferred;
            }

            MustInferArgTypes = false;

            // the lambda was bound before its argument types were known, so that binding has to
            // be discarded and redone
            ctx.ResetExpressionType(this);
        }

        /// <summary>
        /// Interprets the lambda as a particular delegate with given arg & return types.
        /// </summary>
        public void SetInferredReturnType(Context ctx, TypeEntry type)
        {
            ctx.BindingOf<Binding>(this).InferredReturnType = type;
        }

        #endregion

        public override string ToString()
        {
            var arglist = Arguments.Select(x => string.Format("{0}:{1}", x.Name, x.Type != null ? x.Type.Name : x.TypeSignature));
            return string.Format("lambda({0})", string.Join(", ", arglist));
        }
    }
}