using Lens.Compiler;
using Lens.SyntaxTree;

namespace Lens.Utils
{
    internal static class NodeExtensions
    {
        /// <summary>
        /// Emits the node ready for accessing members or invoking methods on it.
        /// </summary>
        public static void EmitNodeForAccess(this NodeBase node, Context ctx)
        {
            // the invocation source is captured while binding, before it is known whether the node
            // expands into something else
            node = ctx.Expanded(node);

            var type = node.Resolve(ctx);

            // a type parameter may be substituted with a value type or with a reference type, and
            // only the members of its constraints can be invoked on it. Boxing is valid for both
            // kinds and lets a single call site serve every instantiation.
            if (ctx.Resolver.IsDeclaredTypeParameter(type))
            {
                node.Emit(ctx, true);
                ctx.CurrentMethod.Generator.EmitBox(type);
                return;
            }

            if (type.IsValueType)
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
                    gen.EmitSaveObject(type);

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