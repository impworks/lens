using Lens.Compiler;
using Lens.SyntaxTree;

namespace Lens.Utils
{
    internal static class NodeExtensions
    {
        /// <summary>
        /// Emits the node ready for accessing members or invoking methods on it.
        /// </summary>
        /// <param name="constrained">
        /// Whether the receiver is about to be used by a call carrying the 'constrained.' prefix.
        /// Such a call takes the receiver's address and dispatches without boxing, which is what a
        /// member reached through a constraint of a type parameter needs; anything else - a field,
        /// a delegate over a method - needs an object reference instead.
        /// </param>
        public static void EmitNodeForAccess(this NodeBase node, Context ctx, bool constrained = false)
        {
            // the invocation source is captured while binding, before it is known whether the node
            // expands into something else
            node = ctx.Expanded(node);

            var type = node.Resolve(ctx);
            var isTypeParameter = ctx.Resolver.IsDeclaredTypeParameter(type);

            // a type parameter may be substituted with a value type or with a reference type, and
            // only the members of its constraints can be invoked on it. Where the call site cannot
            // carry the 'constrained.' prefix, boxing is the only thing valid for both kinds - and
            // it costs nothing when the parameter is known to be a reference type, since boxing one
            // is a no-op.
            if (isTypeParameter && !constrained)
            {
                node.Emit(ctx, true);
                ctx.CurrentMethod.Generator.EmitBox(type.Materialize());
                return;
            }

            // a value type, and a type parameter under the prefix, are both accessed through the
            // receiver's address: for the parameter that is what lets one call site serve every
            // instantiation - the runtime dispatches directly for a value type and dereferences
            // before the callvirt for a reference type - without an allocation and without
            // mutating a copy of the caller's value
            if (type.IsValueType || isTypeParameter)
            {
                if (node is IPointerProvider provider)
                {
                    ctx.RequirePointer(provider);
                    node.Emit(ctx, true);
                }
                else
                {
                    var gen = ctx.CurrentMethod.Generator;

                    var tmpVar = ctx.Scope.DeclareImplicit(ctx, type, true);
                    gen.EmitLoadLocal(tmpVar.LocalBuilder, true);

                    node.Emit(ctx, true);
                    gen.EmitSaveObject(type.Materialize());

                    gen.EmitLoadLocal(tmpVar.LocalBuilder, true);
                }
            }
            else
            {
                node.Emit(ctx, true);
            }
        }
    }
}