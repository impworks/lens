using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Lens.Resolver;

namespace Lens.Compiler
{
    /// <summary>
    /// The pseudo-members the runtime synthesises for arrays of rank greater than one.
    ///
    /// ldelem / stelem / ldlen address a vector and nothing else: a rank-n array keeps its bounds
    /// where a vector keeps its element count, so those opcodes read the header instead of the
    /// data. Every access to such an array goes through Get, Set and Address, which have no
    /// metadata behind them and have to be asked for by shape.
    /// </summary>
    internal static class MultiDimArrays
    {
        #region Members

        /// <summary>
        /// The 'Get' pseudo-method: (int, ..., int) -> element.
        /// </summary>
        public static MethodInfo GetterOf(Context ctx, Type arrayType)
        {
            return MethodOf(ctx, arrayType, "Get", arrayType.GetElementType(), IndexTypes(arrayType));
        }

        /// <summary>
        /// The 'Set' pseudo-method: (int, ..., int, element) -> void.
        /// </summary>
        public static MethodInfo SetterOf(Context ctx, Type arrayType)
        {
            var args = IndexTypes(arrayType).Concat(new[] {arrayType.GetElementType()}).ToArray();
            return MethodOf(ctx, arrayType, "Set", typeof(void), args);
        }

        /// <summary>
        /// The 'Address' pseudo-method: (int, ..., int) -> element&amp;.
        /// </summary>
        public static MethodInfo AddressOf(Context ctx, Type arrayType)
        {
            return MethodOf(ctx, arrayType, "Address", arrayType.GetElementType().MakeByRefType(), IndexTypes(arrayType));
        }

        /// <summary>
        /// One 'int' per dimension.
        /// </summary>
        public static Type[] IndexTypes(Type arrayType)
        {
            return Enumerable.Repeat(typeof(int), arrayType.GetArrayRank()).ToArray();
        }

        #endregion

        #region Emission

        /// <summary>
        /// Emits the creation of a rank &gt; 1 array whose dimension lengths are already on the
        /// stack, deepest dimension last.
        /// </summary>
        public static void EmitCreate(Context ctx, Type arrayType)
        {
            var gen = ctx.CurrentMethod.Generator;
            var rank = arrayType.GetArrayRank();

            if (TypeResolutionContext.IsStable(arrayType))
            {
                gen.Emit(OpCodes.Newobj, arrayType.GetConstructor(IndexTypes(arrayType)));
                return;
            }

            // an array of something that is still being built has no constructor to call: nothing
            // has emitted the type yet, and a token for its .ctor cannot be handed out. The lengths
            // are already on the stack, so they are gathered into an int[] and the runtime is asked
            // for the array by shape instead.
            var sizes = ctx.Scope.DeclareImplicit(ctx, TypeEntryCache.Of<int[]>(), false);
            var length = ctx.Scope.DeclareImplicit(ctx, TypeEntryCache.Of<int>(), false);

            gen.EmitConstant(rank);
            gen.EmitCreateArray(typeof(int));
            gen.EmitSaveLocal(sizes.LocalBuilder);

            // the lengths were pushed outermost first, so they come off innermost first
            for (var idx = rank - 1; idx >= 0; idx--)
            {
                gen.EmitSaveLocal(length.LocalBuilder);

                gen.EmitLoadLocal(sizes.LocalBuilder);
                gen.EmitConstant(idx);
                gen.EmitLoadLocal(length.LocalBuilder);
                gen.EmitSaveIndex(typeof(int));
            }

            gen.Emit(OpCodes.Ldtoken, arrayType.GetElementType());
            gen.EmitCall(typeof(Type).GetMethod("GetTypeFromHandle", new[] {typeof(RuntimeTypeHandle)}));
            gen.EmitLoadLocal(sizes.LocalBuilder);
            gen.EmitCall(typeof(Array).GetMethod("CreateInstance", new[] {typeof(Type), typeof(int[])}));
            gen.Emit(OpCodes.Castclass, arrayType);
        }

        #endregion

        #region Helpers

        private static MethodInfo MethodOf(Context ctx, Type arrayType, string name, Type returnType, Type[] argTypes)
        {
            // an array whose element type is still being built cannot be reflected on at all; the
            // module can still hand out a token for a member the runtime will synthesise later
            if (!TypeResolutionContext.IsStable(arrayType))
                return ctx.MainModule.GetArrayMethod(arrayType, name, CallingConventions.HasThis, returnType, argTypes);

            return arrayType.GetMethod(name, argTypes);
        }

        #endregion
    }
}
