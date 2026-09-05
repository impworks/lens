using Lens.Resolver;
using Lens.Stdlib;
using Lens.SyntaxTree;

namespace Lens.Compiler
{
    /// <summary>
    /// What the compiler knows about System.Index and System.Range.
    ///
    /// The two types are recognised by name rather than held as a Type: they do not exist on every
    /// framework LENS is built for, so nothing here may name them at compile time. A script that
    /// writes '^1' or '1..3' on a runtime without them fails to resolve the type, which is the
    /// truthful answer and the one the resolver gives on its own.
    /// </summary>
    internal static class RangeTypes
    {
        #region Names

        public const string IndexTypeName = "System.Index";
        public const string RangeTypeName = "System.Range";

        /// <summary>
        /// Whether the type is System.Index.
        /// </summary>
        public static bool IsIndex(TypeEntry type)
        {
            return IsNamed(type, IndexTypeName);
        }

        /// <summary>
        /// Whether the type is System.Range.
        /// </summary>
        public static bool IsRange(TypeEntry type)
        {
            return IsNamed(type, RangeTypeName);
        }

        private static bool IsNamed(TypeEntry type, string name)
        {
            return type != null && type.IsValueType && type.FullName == name;
        }

        #endregion

        #region Construction

        /// <summary>
        /// Builds an index counted from the start or from the end of whatever it is applied to.
        /// </summary>
        public static NodeBase NewIndex(NodeBase offset, bool fromEnd)
        {
            return Expr.New(IndexTypeName, offset, Expr.Bool(fromEnd));
        }

        /// <summary>
        /// Builds a range out of two indices.
        /// </summary>
        public static NodeBase NewRange(NodeBase start, NodeBase end)
        {
            return Expr.New(RangeTypeName, start, end);
        }

        /// <summary>
        /// The index of the first element, which is what an omitted lower bound means.
        /// </summary>
        public static NodeBase IndexOfStart()
        {
            return Expr.GetMember((TypeSignature) IndexTypeName, "Start");
        }

        /// <summary>
        /// The index one past the last element, which is what an omitted upper bound means.
        /// </summary>
        public static NodeBase IndexOfEnd()
        {
            return Expr.GetMember((TypeSignature) IndexTypeName, "End");
        }

        #endregion

        #region Reading

        /// <summary>
        /// The offset an index means within a sequence of the given length.
        /// </summary>
        public static NodeBase OffsetOf(NodeBase index, NodeBase length)
        {
            return Expr.Invoke(index, "GetOffset", length);
        }

        /// <summary>
        /// One of a range's two bounds, as a plain offset from the start.
        ///
        /// The range is read twice over, so the expression it comes from has to be a name: there is
        /// no length here to resolve a bound against, and a bound counted from the end is therefore
        /// refused - at runtime, because only then is it known which one was written.
        /// </summary>
        public static NodeBase StartBasedBoundOf(NodeBase range, bool isEnd)
        {
            var name = isEnd ? "End" : "Start";

            return Expr.Invoke(
                Expr.GetMember(typeof(RangeHelper), nameof(RangeHelper.RequireStartBased)),
                Expr.GetMember(Expr.GetMember(range, name), "Value"),
                Expr.GetMember(Expr.GetMember(range, name), "IsFromEnd")
            );
        }

        #endregion
    }
}
