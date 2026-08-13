using System;
using Lens.Compiler;
using Lens.Resolver;

namespace Lens.SyntaxTree.Internals
{
    /// <summary>
    /// Builds a delegate over a parameterless method of the current object.
    ///
    /// A lambda would be the obvious way to write this, and it is the wrong way: a lambda declared
    /// inside a loop gets a closure class of its own, and inside that class 'this' is the closure
    /// rather than the object the lambda was written in. A state machine's continuation has to
    /// reach the machine itself, from wherever in the body the suspension happens to be.
    /// </summary>
    internal class SelfMethodDelegateNode : NodeBase
    {
        #region Constructor

        public SelfMethodDelegateNode(string methodName)
        {
            _methodName = methodName;
        }

        #endregion

        #region Fields

        private readonly string _methodName;

        private MethodWrapper _method;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            _method = ctx.ResolveMethod(ctx.CurrentType.SelfType, _methodName, new TypeEntry[0]);

            return TypeEntryCache.Of(
                FunctionalHelper.CreateDelegateType(_method.ReturnType.Materialize(), TypeEntry.Materialize(_method.ArgumentTypes))
            );
        }

        #endregion

        #region Emit

        protected override void EmitInternal(Context ctx, bool mustReturn)
        {
            var gen = ctx.CurrentMethod.Generator;
            var ctor = ctx.ResolveConstructor(Resolve(ctx), new[] {TypeEntryCache.Of<object>(), TypeEntryCache.Of<IntPtr>()});

            gen.EmitLoadArgument(0);
            gen.EmitLoadFunctionPointer(_method.MethodInfo);
            gen.EmitCreateObject(ctor.ConstructorInfo);
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return $"this.{_methodName}";
        }

        #endregion
    }
}
