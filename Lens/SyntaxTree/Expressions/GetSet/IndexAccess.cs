using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.Stdlib;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Expressions.Instantiation;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.Expressions.GetSet
{
    /// <summary>
    /// An index access whose index is a System.Index or a System.Range, and what it lowers into.
    ///
    /// Neither type is an index anything understands by itself: both are relative to the length of
    /// what is being indexed, and only the access knows what that is. So the access resolves them
    /// against that length and is compiled as the one an integer would have made - which is also
    /// why nothing is built here when the index is written out in place: '^1' inside an access is
    /// the arithmetic that finds the last element, not an Index that is then asked for it.
    ///
    /// A type that offers an indexer of its own for either of the two types is left alone: what it
    /// says its indices mean beats what this would have made of them.
    /// </summary>
    internal class IndexAccess
    {
        #region Constructor

        private IndexAccess()
        {
        }

        #endregion

        #region Fields

        /// <summary>
        /// How a range is taken apart from the sequence it addresses.
        /// </summary>
        private enum SliceKind
        {
            /// <summary>
            /// The type slices itself: Substring, Slice, GetRange.
            /// </summary>
            Method,

            /// <summary>
            /// An array, which is copied.
            /// </summary>
            Array,

            /// <summary>
            /// An IList of T, which is walked.
            /// </summary>
            List
        }

        /// <summary>
        /// Whether the access addresses a whole segment rather than a single element.
        /// </summary>
        public bool IsSlice { get; private set; }

        /// <summary>
        /// What the access yields, or - for an assignment - what the assigned value has to be.
        /// </summary>
        public TypeEntry ResultType { get; private set; }

        /// <summary>
        /// The property that says how many elements the target holds: 'Length' or 'Count'.
        /// </summary>
        private string _lengthMember;

        /// <summary>
        /// The element type of the sequence, where a slice is built out of one.
        /// </summary>
        private TypeEntry _elementType;

        private SliceKind _sliceKind;
        private string _sliceMethod;

        #endregion

        #region Detection

        /// <summary>
        /// Works out how an access is to be compiled, or returns null when its index is an
        /// ordinary one and the access needs nothing done to it.
        /// </summary>
        public static IndexAccess Detect(Context ctx, TypeEntry targetType, List<NodeBase> indexes, bool isGetter, NodeBase owner)
        {
            // a multidimensional array, and any indexer of several arguments, addresses one element
            // by several indices, and neither an index counted from the end nor a range says
            // anything about which of the dimensions it belongs to
            if (indexes.Count != 1)
            {
                CheckOrdinary(ctx, targetType, indexes);
                return null;
            }

            var indexType = indexes[0].Resolve(ctx);
            var isRange = RangeTypes.IsRange(indexType);
            if (!isRange && !RangeTypes.IsIndex(indexType))
                return null;

            // the type may well have an opinion of its own about what an Index or a Range means to
            // it, and it is the one that counts
            if (HasOwnIndexer(ctx, targetType, indexType, isGetter))
                return null;

            var access = new IndexAccess {IsSlice = isRange};

            access._lengthMember = FindLengthMember(ctx, targetType);
            if (access._lengthMember == null)
                throw Error(owner, isRange ? CompilerMessages.RangeIndexNotSupported : CompilerMessages.IndexFromEndNotSupported, targetType);

            if (isRange)
                access.DetectSlice(ctx, targetType, isGetter, owner);
            else
                access.DetectElement(ctx, targetType, isGetter);

            return access;
        }

        /// <summary>
        /// Settles what a single element access yields, or takes.
        /// </summary>
        private void DetectElement(Context ctx, TypeEntry targetType, bool isGetter)
        {
            if (targetType.IsArray)
            {
                ResultType = targetType.ElementType;
                return;
            }

            // the access that survives is the one an integer would have made, so it is that one
            // whose absence has to be reported - and the indexer's own message says it best
            var indexer = ctx.ResolveIndexer(targetType, new[] {TypeEntryCache.Of<int>()}, isGetter);

            ResultType = isGetter
                ? indexer.ReturnType.Dereferenced()
                : indexer.ArgumentTypes[indexer.ArgumentTypes.Length - 1];
        }

        /// <summary>
        /// Settles how a segment is taken out of the target, and what comes of it.
        /// </summary>
        private void DetectSlice(Context ctx, TypeEntry targetType, bool isGetter, NodeBase owner)
        {
            if (targetType.IsVectorArray)
            {
                _sliceKind = SliceKind.Array;
                _elementType = targetType.ElementType;
                ResultType = isGetter ? targetType : SequenceOf(ctx, _elementType);
                return;
            }

            if (isGetter)
            {
                // a type that slices itself does it better than anything here could: a string hands
                // back a string, and a collection hands back its own kind. A slice that is a ref
                // struct is not one of those, because it borrows the storage it was cut from and
                // nothing here may hand that out as a value.
                var method = FindMethod(ctx, targetType, "Slice")
                             ?? FindMethod(ctx, targetType, "Substring")
                             ?? FindMethod(ctx, targetType, "GetRange");

                if (method != null && !method.ReturnType.IsByRefLike)
                {
                    _sliceKind = SliceKind.Method;
                    _sliceMethod = method.Name;
                    ResultType = method.ReturnType;
                    return;
                }
            }

            _elementType = FindListElement(ctx, targetType);
            if (_elementType == null)
                throw Error(owner, CompilerMessages.RangeIndexNotSupported, targetType);

            _sliceKind = SliceKind.List;
            ResultType = isGetter
                ? TypeEntry.Generic(ctx.Resolver, typeof(List<>), _elementType)
                : SequenceOf(ctx, _elementType);
        }

        /// <summary>
        /// Reports an index of either type where the access addresses one element by several
        /// indices, which is the one shape neither of them fits.
        /// </summary>
        private static void CheckOrdinary(Context ctx, TypeEntry targetType, List<NodeBase> indexes)
        {
            foreach (var curr in indexes)
            {
                var type = curr.Resolve(ctx);

                if (RangeTypes.IsRange(type))
                    throw Error(curr, CompilerMessages.RangeIndexNotSupported, targetType);

                if (RangeTypes.IsIndex(type))
                    throw Error(curr, CompilerMessages.IndexFromEndNotSupported, targetType);
            }
        }

        #endregion

        #region Expansion

        /// <summary>
        /// Compiles a read as the access an integer would have made.
        /// </summary>
        public NodeBase ExpandGet(Context ctx, GetIndexNode node)
        {
            if (IsSlice && (node.RefArgumentRequired || ctx.IsPointerRequired(node)))
                throw Error(node, CompilerMessages.RangeIndexRef, node.Expression.Resolve(ctx));

            var body = new CodeBlockNode();
            var target = Hoist(ctx, node.Expression, body);

            if (!IsSlice)
            {
                var getter = Expr.GetIdx(target, OffsetOf(ctx, node.Index, target, false));
                getter.RefArgumentRequired = node.RefArgumentRequired;

                if (ctx.IsPointerRequired(node))
                    ctx.RequirePointer(getter);

                return Complete(body, getter);
            }

            var offset = Segment(ctx, node.Index, target, body, out var count);
            return Complete(body, SliceCall(ctx, target, offset, count));
        }

        /// <summary>
        /// Compiles an assignment as the one an integer would have made.
        /// </summary>
        public NodeBase ExpandSet(Context ctx, SetIndexNode node)
        {
            var body = new CodeBlockNode();
            var target = Hoist(ctx, node.Expression, body);

            if (!IsSlice)
                return Complete(body, Expr.SetIdx(target, OffsetOf(ctx, node.Index, target, false), node.Value));

            var offset = Segment(ctx, node.Index, target, body, out var count);
            return Complete(body, ReplaceCall(ctx, target, offset, count, node.Value));
        }

        /// <summary>
        /// The offset a range begins at, together with how many elements it covers.
        ///
        /// The beginning is read twice over - once as itself, once to work out the count - so it is
        /// reduced to a name first, whatever it took to arrive at it.
        /// </summary>
        private NodeBase Segment(Context ctx, NodeBase index, NodeBase target, CodeBlockNode body, out NodeBase count)
        {
            NodeBase startIndex, endIndex;

            var range = index as RangeNode;
            if (range != null)
            {
                startIndex = range.Start;
                endIndex = range.End;
            }
            else
            {
                // a range that arrives as a value is asked for both of its bounds, so it too has to
                // be a name before either of them is read
                var value = Hoist(ctx, index, body);
                startIndex = Expr.GetMember(value, "Start");
                endIndex = Expr.GetMember(value, "End");
            }

            var offset = ctx.Scope.DeclareImplicit(ctx, TypeEntryCache.Of<int>(), false);
            body.Add(Expr.Set(offset, OffsetOf(ctx, startIndex, target, false)));

            count = Expr.Sub(OffsetOf(ctx, endIndex, target, true), Expr.Get(offset));
            return Expr.Get(offset);
        }

        /// <summary>
        /// The offset an index means within the target, as an integer.
        /// </summary>
        private NodeBase OffsetOf(Context ctx, NodeBase index, NodeBase target, bool isEnd)
        {
            // a bound left out is the beginning or the end of the target itself
            if (index == null)
                return isEnd ? LengthOf(target) : Expr.Int(0);

            // '^k' is written out here and now, and no Index is built to be asked what it means
            var fromEnd = index as IndexFromEndNode;
            if (fromEnd != null)
                return Expr.Sub(LengthOf(target), fromEnd.Operand);

            return RangeTypes.IsIndex(index.Resolve(ctx))
                ? RangeTypes.OffsetOf(index, LengthOf(target))
                : index;
        }

        /// <summary>
        /// How many elements the target holds.
        /// </summary>
        private NodeBase LengthOf(NodeBase target)
        {
            return Expr.GetMember(target, _lengthMember);
        }

        /// <summary>
        /// Builds the call that copies a segment out of the target.
        /// </summary>
        private NodeBase SliceCall(Context ctx, NodeBase target, NodeBase offset, NodeBase count)
        {
            switch (_sliceKind)
            {
                case SliceKind.Method:
                    return Expr.Invoke(target, _sliceMethod, offset, count);

                case SliceKind.Array:
                    return Expr.Invoke(
                        Expr.GetMember(typeof(RangeHelper), nameof(RangeHelper.GetArrayRange)),
                        target,
                        offset,
                        count
                    );

                default:
                    return Expr.Invoke(
                        Expr.GetMember(typeof(RangeHelper), nameof(RangeHelper.GetListRange)),
                        Expr.Cast(target, ListOf(ctx, _elementType)),
                        offset,
                        count
                    );
            }
        }

        /// <summary>
        /// Builds the call that stores a sequence of values over a segment of the target.
        /// </summary>
        private NodeBase ReplaceCall(Context ctx, NodeBase target, NodeBase offset, NodeBase count, NodeBase value)
        {
            var values = Expr.Cast(value, SequenceOf(ctx, _elementType));

            return _sliceKind == SliceKind.Array
                ? Expr.Invoke(
                    Expr.GetMember(typeof(RangeHelper), nameof(RangeHelper.ReplaceArrayRange)),
                    target,
                    offset,
                    count,
                    values
                )
                : Expr.Invoke(
                    Expr.GetMember(typeof(RangeHelper), nameof(RangeHelper.ReplaceListRange)),
                    Expr.Cast(target, ListOf(ctx, _elementType)),
                    offset,
                    count,
                    values
                );
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Reduces an expression to a name, so that the access can read it more than once.
        ///
        /// A name is left as it is, and not only to save a copy: a value type kept in a temporary
        /// is a copy, and an assignment through one would be lost. This is the same trade the
        /// shorthand assignment makes, and for the same reason.
        /// </summary>
        private static NodeBase Hoist(Context ctx, NodeBase expr, CodeBlockNode body)
        {
            if (expr is GetIdentifierNode || expr.IsConstant)
                return expr;

            var local = ctx.Scope.DeclareImplicit(ctx, expr.Resolve(ctx), false);
            body.Add(Expr.Set(local, expr));
            return Expr.Get(local);
        }

        /// <summary>
        /// Wraps the access in whatever had to be evaluated before it, and in nothing at all when
        /// there was nothing.
        /// </summary>
        private static NodeBase Complete(CodeBlockNode body, NodeBase access)
        {
            if (body.Statements.Count == 0)
                return access;

            body.Add(access);
            return body;
        }

        /// <summary>
        /// Whether the target type indexes itself by the given type.
        /// </summary>
        private static bool HasOwnIndexer(Context ctx, TypeEntry targetType, TypeEntry indexType, bool isGetter)
        {
            if (targetType.IsArray)
                return false;

            try
            {
                return ctx.ResolveIndexer(targetType, new[] {indexType}, isGetter) != null;
            }
            catch (LensCompilerException)
            {
                return false;
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// The name of the property that says how many elements the type holds, or null when it
        /// says nothing of the kind and no index can be resolved against it.
        /// </summary>
        private static string FindLengthMember(Context ctx, TypeEntry type)
        {
            // a rank > 1 array has a Length, but it is the number of elements of all its dimensions
            // together, and no single index addresses one of those
            if (type.IsArray)
                return type.IsVectorArray ? "Length" : null;

            if (HasIntProperty(ctx, type, "Length"))
                return "Length";

            return HasIntProperty(ctx, type, "Count") ? "Count" : null;
        }

        /// <summary>
        /// Whether the type has a readable integer property of the given name.
        /// </summary>
        private static bool HasIntProperty(Context ctx, TypeEntry type, string name)
        {
            try
            {
                var property = ctx.ResolveProperty(type, name);
                return property.CanGet && property.PropertyType.Is<int>();
            }
            catch (KeyNotFoundException)
            {
                return false;
            }
            catch (LensCompilerException)
            {
                return false;
            }
        }

        /// <summary>
        /// The element type of the list the target is, or null when it is not one.
        /// </summary>
        private static TypeEntry FindListElement(Context ctx, TypeEntry type)
        {
            var ifaces = type.GetInterfaces(ctx.Resolver);
            if (type.IsInterface)
                ifaces = ifaces.Union(new[] {type}).ToArray();

            var list = ifaces.FirstOrDefault(i => i.IsGenericType && i.GetGenericDefinition().Is(typeof(IList<>)));
            return list?.GenericArguments[0];
        }

        /// <summary>
        /// Finds a method that takes an offset and a count, or null when there is none.
        /// </summary>
        private static MethodWrapper FindMethod(Context ctx, TypeEntry type, string name)
        {
            var intType = TypeEntryCache.Of<int>();

            try
            {
                return ctx.ResolveMethod(type, name, new[] {intType, intType});
            }
            catch (LensCompilerException)
            {
                return null;
            }
            catch (KeyNotFoundException)
            {
                return null;
            }
        }

        private static TypeEntry ListOf(Context ctx, TypeEntry element)
        {
            return TypeEntry.Generic(ctx.Resolver, typeof(IList<>), element);
        }

        private static TypeEntry SequenceOf(Context ctx, TypeEntry element)
        {
            return TypeEntry.Generic(ctx.Resolver, typeof(IEnumerable<>), element);
        }

        private static LensCompilerException Error(LocationEntity entity, string message, params object[] args)
        {
            return new LensCompilerException(string.Format(message, args), entity);
        }

        #endregion
    }
}
