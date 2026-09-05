using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lens.Compiler;
using Lens.Resolver;
using Lens.SyntaxTree.Expressions.Instantiation;
using Lens.Translations;
using Lens.Utils;

namespace Lens.SyntaxTree.ControlFlow
{
    internal class ForeachNode : NodeBase
    {
        #region Fields

        /// <summary>
        /// A variable to assign current item to.
        /// </summary>
        public string VariableName { get; set; }

        /// <summary>
        /// Where the loop variable's name is written, for an editor that has to point at it.
        /// </summary>
        public LocationEntity VariableLocation { get; set; }

        /// <summary>
        /// Explicitly specified local variable.
        /// </summary>
        public Local Local { get; set; }

        /// <summary>
        /// A single expression of iterable type.
        /// Must be set to X if the loop is defined like (for A in X)
        /// </summary>
        public NodeBase IterableExpression { get; set; }

        /// <summary>
        /// The lower limit of loop range.
        /// Must be set to X if the loop is defined like (for A in X..Y)
        /// </summary>
        public NodeBase RangeStart { get; set; }

        /// <summary>
        /// The upper limit of loop range.
        /// Must be set to Y if the loop is defined like (for A in X..Y)
        /// </summary>
        public NodeBase RangeEnd { get; set; }

        public CodeBlockNode Body { get; set; }

        private TypeEntry _variableType;
        private Local _variable;
        private TypeEntry _enumeratorType;
        private PropertyWrapper _currentProperty;

        /// <summary>
        /// The type of the range the loop is handed, when it is handed one rather than a sequence.
        /// </summary>
        private TypeEntry _rangeType;

        #endregion

        #region Resolve

        protected override TypeEntry ResolveInternal(Context ctx, bool mustReturn)
        {
            CheckRangeBounds();

            if (IterableExpression != null)
            {
                // a range is not a sequence - it holds two indices and no elements - but it is what
                // the two ends of a loop are, and a loop over one walks the numbers between them
                var iterableType = IterableExpression.Resolve(ctx);
                if (RangeTypes.IsRange(iterableType))
                {
                    _rangeType = iterableType;
                    _variableType = TypeEntryCache.Of<int>();
                }
                else
                {
                    DetectEnumerableType(ctx);
                }
            }
            else
            {
                DetectRangeType(ctx);
            }

            if (VariableName != null && ctx.Scope.FindLocal(VariableName) != null)
                throw new LensCompilerException(string.Format(CompilerMessages.VariableDefined, VariableName));

            if (Local == null)
            {
                // the name the loop declares is created here rather than when the loop is
                // expanded, because the body is bound here: the uses it records have to land on
                // the very name the expansion goes on to declare, or the loop variable would be a
                // name nobody mentions - which is a rename that changes the declaration and
                // leaves every use of it behind
                _variable = new Local(VariableName, _variableType) {Declaration = VariableLocation};
                return Scope.WithTempLocals(ctx, () => Body.Resolve(ctx, mustReturn), _variable);
            }

            // index local specified explicitly: no need to account for it in pre-resolve
            return Body.Resolve(ctx, mustReturn);
        }

        #endregion

        #region Transform

        protected override NodeBase Expand(Context ctx, bool mustReturn)
        {
            if (IterableExpression != null)
            {
                if (_rangeType != null)
                    return ExpandRangeValue(ctx);

                var type = IterableExpression.Resolve(ctx);
                if (type.IsVectorArray)
                    return ExpandArray(ctx);

                return ExpandEnumerable(ctx, mustReturn);
            }

            return ExpandRange(ctx, RangeStart, RangeEnd);
        }

        internal override IEnumerable<NodeChild> GetChildren()
        {
            if (IterableExpression != null)
            {
                yield return new NodeChild(IterableExpression);
            }
            else
            {
                yield return new NodeChild(RangeStart);
                yield return new NodeChild(RangeEnd);
            }

            yield return new NodeChild(Body);
        }

        /// <summary>
        /// Expands the foreach loop if it iterates over an IEnumerable`1.
        /// </summary>
        private NodeBase ExpandEnumerable(Context ctx, bool mustReturn)
        {
            var iteratorVar = ctx.Scope.DeclareImplicit(ctx, _enumeratorType, false);
            var enumerableType = _enumeratorType.IsGenericType
                ? TypeEntry.Generic(ctx.Resolver, typeof(IEnumerable<>), _enumeratorType.GenericArguments[0])
                : TypeEntryCache.Of<IEnumerable>();

            var init = Expr.Set(
                iteratorVar,
                Expr.Invoke(
                    Expr.Cast(IterableExpression, enumerableType),
                    "GetEnumerator"
                )
            );

            var loop = Loop(
                Expr.Invoke(Expr.Get(iteratorVar), "MoveNext"),
                Expr.Block(
                    GetIndexAssignment(CurrentItem(Expr.GetMember(Expr.Get(iteratorVar), "Current"))),
                    Body
                )
            );

            if (_enumeratorType.Implements(ctx.Resolver, TypeEntryCache.Of<IDisposable>(), false))
            {
                var dispose = Expr.Block(Expr.Invoke(Expr.Get(iteratorVar), "Dispose"));
                var returnType = Resolve(ctx);
                var saveLast = mustReturn && !returnType.IsVoid();

                if (saveLast)
                {
                    var resultVar = ctx.Scope.DeclareImplicit(ctx, _enumeratorType, false);
                    return Expr.Block(
                        Expr.Try(
                            Expr.Block(
                                init,
                                Expr.Set(resultVar, loop)
                            ),
                            dispose
                        ),
                        Expr.Get(resultVar)
                    );
                }

                return Expr.Try(
                    Expr.Block(init, loop),
                    dispose
                );
            }

            return Expr.Block(
                init,
                loop
            );
        }

        /// <summary>
        /// Narrows the enumerator's Current to the type the loop variable was given.
        ///
        /// The two differ only for a multidimensional array, whose enumerator is the untyped one
        /// and hands back an object.
        /// </summary>
        private NodeBase CurrentItem(NodeBase current)
        {
            return _currentProperty != null && !_currentProperty.PropertyType.Equals(_variableType)
                ? Expr.Cast(current, _variableType)
                : current;
        }

        /// <summary>
        /// Expands the foreach loop if it iterates over T[].
        /// </summary>
        private NodeBase ExpandArray(Context ctx)
        {
            var arrayVar = ctx.Scope.DeclareImplicit(ctx, IterableExpression.Resolve(ctx), false);
            var idxVar = ctx.Scope.DeclareImplicit(ctx, TypeEntryCache.Of<int>(), false);
            var lenVar = ctx.Scope.DeclareImplicit(ctx, TypeEntryCache.Of<int>(), false);

            return Expr.Block(
                Expr.Set(idxVar, Expr.Int(0)),
                Expr.Set(arrayVar, IterableExpression),
                Expr.Set(lenVar, Expr.GetMember(Expr.Get(arrayVar), "Length")),
                Loop(
                    Expr.Less(
                        Expr.Get(idxVar),
                        Expr.Get(lenVar)
                    ),
                    Expr.Block(
                        GetIndexAssignment(
                            Expr.GetIdx(
                                Expr.Get(arrayVar),
                                Expr.Get(idxVar)
                            )
                        ),
                        Expr.Set(
                            idxVar,
                            Expr.Add(Expr.Get(idxVar), Expr.Int(1))
                        ),
                        Body
                    )
                )
            );
        }

        /// <summary>
        /// Expands the foreach loop if it iterates over a range handed to it as a value.
        ///
        /// Both of the range's bounds are settled before the loop starts, and both have to be
        /// counted from the start: a range on its own is not applied to anything, so there is
        /// nothing for a bound counted from the end to be counted from. Which one was written is
        /// only known once it is there, so that is where it is refused.
        /// </summary>
        private NodeBase ExpandRangeValue(Context ctx)
        {
            var rangeVar = ctx.Scope.DeclareImplicit(ctx, _rangeType, false);
            var startVar = ctx.Scope.DeclareImplicit(ctx, _variableType, false);
            var endVar = ctx.Scope.DeclareImplicit(ctx, _variableType, false);

            return Expr.Block(
                Expr.Set(rangeVar, IterableExpression),
                Expr.Set(startVar, RangeTypes.StartBasedBoundOf(Expr.Get(rangeVar), false)),
                Expr.Set(endVar, RangeTypes.StartBasedBoundOf(Expr.Get(rangeVar), true)),
                ExpandRange(ctx, Expr.Get(startVar), Expr.Get(endVar))
            );
        }

        /// <summary>
        /// Expands the foreach loop if it iterates over a numeric range.
        /// </summary>
        private NodeBase ExpandRange(Context ctx, NodeBase rangeStart, NodeBase rangeEnd)
        {
            var signVar = ctx.Scope.DeclareImplicit(ctx, _variableType, false);
            var idxVar = ctx.Scope.DeclareImplicit(ctx, _variableType, false);

            return Expr.Block(
                Expr.Set(idxVar, rangeStart),
                Expr.Set(
                    signVar,
                    Expr.Invoke(
                        "Math",
                        "Sign",
                        Expr.Sub(rangeEnd, Expr.Get(idxVar))
                    )
                ),
                Loop(
                    Expr.NotEqual(Expr.Get(idxVar), rangeEnd),
                    Expr.Block(
                        GetIndexAssignment(Expr.Get(idxVar)),
                        Body,
                        Expr.Set(
                            idxVar,
                            Expr.Add(
                                Expr.Get(idxVar),
                                Expr.Get(signVar)
                            )
                        )
                    )
                )
            );
        }

        /// <summary>
        /// Builds the loop this 'for' expands into, keeping the place in the source that the
        /// 'for' itself occupies.
        ///
        /// The loop is otherwise anonymous: neither it nor the condition it tests was written by
        /// anyone, so a debugger would have nothing to stop on once per iteration, and stepping
        /// round the loop would look like standing still on its first statement.
        /// </summary>
        private WhileNode Loop(NodeBase condition, CodeBlockNode body)
        {
            var loop = Expr.While(condition, body);

            loop.StartLocation = StartLocation;
            loop.EndLocation = EndLocation;

            return loop;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Reports a bound of the range the loop walks that it can be seen not to have.
        ///
        /// A loop walks the numbers between two ends and is applied to nothing, so a bound counted
        /// from the end has nothing to be counted from, and one left out is not there to begin
        /// with. Where the range is written out, that is visible here; where it arrives as a value,
        /// only the value knows, and it is refused when it gets there.
        /// </summary>
        private void CheckRangeBounds()
        {
            var range = IterableExpression as RangeNode;
            if (range != null)
            {
                CheckRangeBound(range.Start, range);
                CheckRangeBound(range.End, range);
            }
            else if (IterableExpression == null)
            {
                CheckRangeBound(RangeStart, this);
                CheckRangeBound(RangeEnd, this);
            }
        }

        /// <summary>
        /// Reports a bound written as '^k', or not written at all.
        /// </summary>
        private void CheckRangeBound(NodeBase bound, LocationEntity fallback)
        {
            if (bound == null || bound is IndexFromEndNode)
                Error(bound ?? fallback, CompilerMessages.ForeachRangeNotStartBased);
        }

        /// <summary>
        /// Calculates the variable type and other required values for enumeration of an IEnumerable`1.
        /// </summary>
        private void DetectEnumerableType(Context ctx)
        {
            var seqType = IterableExpression.Resolve(ctx);
            if (seqType.IsVectorArray)
            {
                _variableType = seqType.ElementType;
                return;
            }

            // a rank > 1 array is only a bare IEnumerable, but it still knows what it holds:
            // the loop reads it through the untyped enumerator and unwraps every item
            if (seqType.IsArray)
            {
                _enumeratorType = TypeEntryCache.Of<IEnumerator>();
                _currentProperty = ctx.ResolveProperty(_enumeratorType, "Current");
                _variableType = seqType.ElementType;
                return;
            }

            var ifaces = seqType.GetInterfaces(ctx.Resolver);
            if (seqType.IsInterface)
                ifaces = ifaces.Union(new[] {seqType}).ToArray();

            var generic = ifaces.FirstOrDefault(i => i.IsGenericType && i.GetGenericDefinition().Is(typeof(IEnumerable<>)));
            if (generic != null)
                _enumeratorType = TypeEntry.Generic(ctx.Resolver, typeof(IEnumerator<>), generic.GenericArguments[0]);

            else if (ifaces.Contains(TypeEntryCache.Of<IEnumerable>()))
                _enumeratorType = TypeEntryCache.Of<IEnumerator>();

            else
                Error(IterableExpression, CompilerMessages.TypeNotIterable, seqType);

            _currentProperty = ctx.ResolveProperty(_enumeratorType, "Current");
            _variableType = _currentProperty.PropertyType;
        }

        /// <summary>
        /// Calculates the variable type of a numeric range iteration.
        /// </summary>
        private void DetectRangeType(Context ctx)
        {
            var t1 = RangeStart.Resolve(ctx);
            var t2 = RangeEnd.Resolve(ctx);

            if (t1 != t2)
                Error(CompilerMessages.ForeachRangeTypeMismatch, t1, t2);

            if (!t1.IsIntegerType())
                Error(CompilerMessages.ForeachRangeNotInteger, t1);

            _variableType = t1;
        }

        /// <summary>
        /// Gets the expression for saving the value at an index to a variable.
        /// </summary>
        private NodeBase GetIndexAssignment(NodeBase indexGetter)
        {
            return Local == null
                ? Expr.DeclareLet(_variable, indexGetter)
                : Expr.Set(Local, indexGetter) as NodeBase;
        }

        #endregion

        #region Debug

        protected bool Equals(ForeachNode other)
        {
            return string.Equals(VariableName, other.VariableName)
                   && Equals(IterableExpression, other.IterableExpression)
                   && Equals(RangeStart, other.RangeStart)
                   && Equals(RangeEnd, other.RangeEnd)
                   && Equals(Body, other.Body);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ForeachNode) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (VariableName != null ? VariableName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (IterableExpression != null ? IterableExpression.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (RangeStart != null ? RangeStart.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (RangeEnd != null ? RangeEnd.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Body != null ? Body.GetHashCode() : 0);
                return hashCode;
            }
        }

        #endregion
    }
}