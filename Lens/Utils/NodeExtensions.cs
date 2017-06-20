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
            var type = node.Resolve(ctx);

            if (type.IsValueType)
            {
                if (node is IPointerProvider)
                {
                    (node as IPointerProvider).PointerRequired = true;
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