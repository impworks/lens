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

        #region Fields

        /// <summary>
        /// Backing method reference.
        /// </summary>
        private MethodEntity _method;

        /// <summary>
        /// Return type inferred from context.
        /// </summary>
        private Type _inferredReturnType;

        /// <summary>
        /// Flag indicating that current lambda has arguments with omitted types and they must be resolved from the context.
        /// </summary>
        public bool MustInferArgTypes { get; private set; }

        #endregion

        #region Resolve

        protected override Type ResolveInternal(Context ctx, bool mustReturn)
        {
            var argTypes = new List<Type>();
            foreach (var curr in Arguments)
            {
                if (curr.IsVariadic)
                    Error(CompilerMessages.VariadicArgumentLambda);

                var type = curr.GetArgumentType(ctx);
                argTypes.Add(type);

                if (type == typeof(UnspecifiedType))
                    MustInferArgTypes = true;
            }

            if (MustInferArgTypes)
                return FunctionalHelper.CreateLambdaType(argTypes.ToArray());

            Body.Scope.RegisterArguments(ctx, false, Arguments);

            var retType = Body.Resolve(ctx);
            return FunctionalHelper.CreateDelegateType(retType, argTypes.ToArray());
        }

        #endregion

        #region Process closures

        public override void ProcessClosures(Context ctx)
        {
            if (MustInferArgTypes)
            {
                var name = Arguments.First(a => a.Type == typeof(UnspecifiedType)).Name;
                Error(CompilerMessages.LambdaArgTypeUnknown, name);
            }

            // get evaluated return type
            var retType = _inferredReturnType ?? Body.Resolve(ctx);
            if (retType == typeof(NullType))
                Error(CompilerMessages.LambdaReturnTypeUnknown);
            if (retType.IsVoid())
                retType = typeof(void);

            _method = ctx.Scope.CreateClosureMethod(ctx, Arguments, retType);
            _method.Body = Body;

            var outerMethod = ctx.CurrentMethod;
            ctx.CurrentMethod = _method;

            _method.Body.ProcessClosures(ctx);

            ctx.CurrentMethod = outerMethod;
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;

            // find constructor
            var type = FunctionalHelper.CreateDelegateType(Body.Resolve(ctx), _method.ArgumentTypes);
            var ctor = ctx.ResolveConstructor(type, new[] {typeof(object), typeof(IntPtr)});

            var closureInstance = ctx.Scope.ActiveClosure.ClosureVariable;
            gen.EmitLoadLocal(closureInstance);
            gen.EmitLoadFunctionPointer(_method.MethodBuilder);
            gen.EmitCreateObject(ctor.ConstructorInfo);
        }

        #endregion

        #region Argument type detection

        /// <summary>
        /// Sets correct types for arguments which are inferred from usage (invocation, assignment, type casting).
        /// </summary>
        public void SetInferredArgumentTypes(Type[] argTypes)
        {
            if (Arguments.Count != argTypes.Length)
                Error(CompilerMessages.LambdaArgumentsCountMismatch, argTypes.Length, Arguments.Count);

            for (var idx = 0; idx < argTypes.Length; idx++)
            {
                var inferred = argTypes[idx];
                if (inferred == typeof(UnspecifiedType))
                    Error(CompilerMessages.LambdaArgTypeUnknown, Arguments[idx].Name);

#if DEBUG
                var specified = Arguments[idx].Type;
                if (specified != typeof(UnspecifiedType) && specified != inferred)
                    throw new InvalidOperationException($"Argument type differs: specified '{specified}', inferred '{inferred}'!");
#endif

                Arguments[idx].Type = inferred;
            }

            MustInferArgTypes = false;
            CachedExpressionType = null;
        }

        /// <summary>
        /// Interprets the lambda as a particular delegate with given arg & return types.
        /// </summary>
        public void SetInferredReturnType(Type type)
        {
            _inferredReturnType = type;
        }

        #endregion

        public override string ToString()
        {
            var arglist = Arguments.Select(x => string.Format("{0}:{1}", x.Name, x.Type != null ? x.Type.Name : x.TypeSignature));
            return string.Format("lambda({0})", string.Join(", ", arglist));
        }
    }
}