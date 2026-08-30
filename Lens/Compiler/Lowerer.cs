using System;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree;
using Lens.SyntaxTree.ControlFlow;
using Lens.SyntaxTree.Declarations;
using Lens.SyntaxTree.Declarations.Locals;
using Lens.SyntaxTree.Expressions.GetSet;
using Lens.SyntaxTree.PatternMatching;
using Lens.SyntaxTree.Declarations.Functions;
using Lens.SyntaxTree.Internals;
using Lens.Compiler.Entities;
using Lens.Translations;

namespace Lens.Compiler
{
    /// <summary>
    /// Rewrites structured control flow into a flat list of statements over an explicit label and
    /// jump vocabulary.
    ///
    /// LENS compiles the syntax tree straight to IL: a while node emits its own loop, an if node
    /// emits its own branches. A state machine needs the opposite shape, because a resume point has
    /// to be a jump target and nothing can jump into the middle of a node that emits itself.
    ///
    /// The pass rewrites rather than mutates: a node that has to be flattened is replaced by the
    /// statements that flatten it, and every node that does not is reused as it stands. Nothing the
    /// parser produced is written to.
    ///
    /// Blocks are kept nested. Only the control flow between them is flattened, so a variable
    /// declared in a loop body still belongs to that body's frame - the pass never has to merge two
    /// frames, and therefore never has to rename anything.
    /// </summary>
    internal partial class Lowerer
    {
        #region Constructor

        public Lowerer(Context ctx, Action<NodeBase, ResumePoint, List<NodeBase>> emitYield = null, Func<NodeBase, ResumePoint, List<NodeBase>, NodeBase> emitAwait = null, Func<LabelRef, bool, NodeBase> emitUnwind = null, LabelRef unwind = null, bool lowerEverything = false)
        {
            _ctx = ctx;
            _emitYield = emitYield;
            _emitAwait = emitAwait;
            _emitUnwind = emitUnwind;
            _lowerEverything = lowerEverything;

            Body = new LoweredRegion(null, "body", unwind);
            _region = Body;
        }

        #endregion

        #region Fields

        private readonly Context _ctx;

        /// <summary>
        /// Appends the statements that hand one value over to the consumer of an iterator.
        /// Null when the pass is being used without a state machine, which is how it is validated.
        /// </summary>
        private readonly Action<NodeBase, ResumePoint, List<NodeBase>> _emitYield;

        /// <summary>
        /// Appends the statements that suspend the function until an operation finishes, and
        /// returns the expression that reads the operation's result.
        /// </summary>
        private readonly Func<NodeBase, ResumePoint, List<NodeBase>, NodeBase> _emitAwait;

        /// <summary>
        /// Builds the jump that carries on unwinding when the machine is being abandoned: an
        /// iterator that is disposed half-way still owes its finally blocks a run.
        /// Null for a machine nobody can abandon.
        /// </summary>
        private readonly Func<LabelRef, bool, NodeBase> _emitUnwind;

        /// <summary>
        /// Whether every construct is to be flattened, rather than only the ones that contain a
        /// resume point. Only a test asks for this: it is how the pass is checked to preserve
        /// behaviour on code that has nothing to do with state machines.
        /// </summary>
        private readonly bool _lowerEverything;

        private int _labelId;
        private int _stateId;

        /// <summary>
        /// The region a resume point met right now would belong to.
        /// </summary>
        private LoweredRegion _region;

        /// <summary>
        /// The name a bare 'throw' means while a hoisted handler body is being lowered.
        /// </summary>
        private string _rethrowVariable;

        /// <summary>
        /// The method body itself, seen as the outermost region.
        /// </summary>
        public readonly LoweredRegion Body;

        /// <summary>
        /// Where a suspension goes. Leaving is how a machine gets out of MoveNext from anywhere,
        /// protected region or not, and the actual return happens at this label.
        /// </summary>
        public readonly LabelRef SuspendLabel = new LabelRef("suspend");

        /// <summary>
        /// The statements that send a resuming machine to the point it stopped at.
        /// Only meaningful once the body has been lowered.
        /// </summary>
        public List<NodeBase> RootDispatch()
        {
            return BuildDispatch(Body);
        }

        #endregion

        #region Entry point

        /// <summary>
        /// Rewrites a method body. The body is a statement list, so nothing in it is in value
        /// position except its own last statement.
        /// </summary>
        public CodeBlockNode Lower(CodeBlockNode body, bool valuePosition)
        {
            return LowerBlock(body, valuePosition);
        }

        #endregion

        #region Blocks and statements

        private CodeBlockNode LowerBlock(CodeBlockNode block, bool valuePosition)
        {
            var lastIdx = block.Statements.FindLastIndex(x => !(x is IMetaNode));

            var output = new List<NodeBase>();
            for (var idx = 0; idx < block.Statements.Count; idx++)
                LowerStatement(block.Statements[idx], valuePosition && idx == lastIdx, output);

            var result = new CodeBlockNode(block.ScopeKind);
            result.AddRange(output);
            CopyLocation(block, result);
            return result;
        }

        private void LowerStatement(NodeBase stmt, bool valuePosition, List<NodeBase> output)
        {
            if (stmt is YieldNode yield)
            {
                LowerYield(yield, output);
                return;
            }

            var suspends = ContainsResumePoint(stmt);
            if (!suspends && !_lowerEverything)
            {
                output.Add(stmt);
                return;
            }

            // a construct in value position is not flattened: its value would have to survive an
            // arbitrary jump, and there is nowhere for it to wait - what a flattened branch leaves
            // on the stack is not there when another branch arrives at the same label. A block is
            // the exception, because it does not branch: its value is its last statement's, and
            // that statement is in value position in turn
            if (valuePosition)
            {
                if (stmt is CodeBlockNode valueBlock)
                {
                    output.Add(LowerBlock(valueBlock, true));
                    return;
                }
            }
            else
            {
                switch (stmt)
                {
                    case CodeBlockNode block:
                        output.Add(LowerBlock(block, false));
                        return;

                    case IfNode node:
                        LowerIf(node, output);
                        return;

                    case WhileNode node:
                        LowerWhile(node, output);
                        return;

                    case ForeachNode node:
                        LowerForeach(node, output);
                        return;

                    case TryNode node:
                        LowerTry(node, output);
                        return;

                    case UsingNode node:
                        LowerUsing(node, output);
                        return;

                    case MatchNode node:
                        LowerMatch(node, output);
                        return;

                    case ThrowNode node when node.Expression == null && _rethrowVariable != null:
                        // the handler body no longer sits in the protected region it was written
                        // in, so there is nothing for a bare rethrow to pick the exception up from
                        output.Add(Expr.Throw(Expr.Get(_rethrowVariable)));
                        return;
                }
            }

            if (!suspends)
            {
                output.Add(stmt);
                return;
            }

            // an ordinary statement that happens to suspend somewhere inside: the awaits are lifted
            // out of it, and it is rebuilt around the names that hold their results
            var spilled = Spill(stmt, output);
            output.Add(spilled);
        }

        #endregion

        #region Control structures

        /// <summary>
        ///     goto else unless cond
        ///     [true branch]
        ///     goto end
        /// else:
        ///     [false branch]
        /// end:
        /// </summary>
        private void LowerIf(IfNode node, List<NodeBase> output)
        {
            var elseLabel = NewLabel("else");

            // the condition is decided before either branch is, so a suspension in it is one that
            // happens before the branch - an ordinary statement's worth of rewriting
            output.Add(new GotoNode(elseLabel, Spill(node.Condition, output), false));
            output.Add(LowerBlock(node.TrueAction, false));

            if (node.FalseAction == null)
            {
                output.Add(new LabelNode(elseLabel));
                return;
            }

            var endLabel = NewLabel("endif");
            output.Add(new GotoNode(endLabel));
            output.Add(new LabelNode(elseLabel));
            output.Add(LowerBlock(node.FalseAction, false));
            output.Add(new LabelNode(endLabel));
        }

        /// <summary>
        /// begin:
        ///     goto end unless cond
        ///     [body]
        ///     goto begin
        /// end:
        /// </summary>
        private void LowerWhile(WhileNode node, List<NodeBase> output)
        {
            var beginLabel = NewLabel("while");
            var endLabel = NewLabel("endwhile");

            // the condition is decided again on each turn of the loop, so whatever it takes to
            // decide it belongs after the label the loop comes back to
            output.Add(new LabelNode(beginLabel));
            output.Add(new GotoNode(endLabel, Spill(node.Condition, output), false));
            output.Add(LowerBlock(node.Body, false));
            output.Add(new GotoNode(beginLabel));
            output.Add(new LabelNode(endLabel));
        }

        private void LowerForeach(ForeachNode node, List<NodeBase> output)
        {
            if (node.IterableExpression != null)
                LowerForeachOverSequence(node, output);
            else
                LowerForeachOverRange(node, output);
        }

        /// <summary>
        ///     var e = enumerator(seq)
        /// begin:
        ///     goto end unless e.MoveNext()
        ///     { let x = e.Current; [body] }
        ///     goto begin
        /// end:
        ///     dispose e
        /// </summary>
        private void LowerForeachOverSequence(ForeachNode node, List<NodeBase> output)
        {
            var beginLabel = NewLabel("for");
            var endLabel = NewLabel("endfor");
            var iterator = _ctx.Unique.TempVariableName();

            output.Add(Expr.Var(iterator, new GetEnumeratorNode(Spill(node.IterableExpression, output))));
            output.Add(new LabelNode(beginLabel));
            output.Add(new GotoNode(endLabel, Expr.Invoke(Expr.Get(iterator), "MoveNext"), false));
            output.Add(
                LoopBody(
                    node,
                    Expr.GetMember(Expr.Get(iterator), "Current")
                )
            );
            output.Add(new GotoNode(beginLabel));
            output.Add(new LabelNode(endLabel));

            // there is no try/finally here, and so no disposal on an abrupt exit. A state machine
            // cannot resume into a protected region, and making one work is the whole of the
            // try/finally story this phase deliberately left for later.
            output.Add(new DisposeNode(Expr.Get(iterator)));
        }

        /// <summary>
        ///     var i = from
        ///     var step = sign(to - i)
        /// begin:
        ///     goto end if i == to
        ///     { let x = i; [body] }
        ///     i = i + step
        ///     goto begin
        /// end:
        /// </summary>
        private void LowerForeachOverRange(ForeachNode node, List<NodeBase> output)
        {
            var beginLabel = NewLabel("for");
            var endLabel = NewLabel("endfor");
            var index = _ctx.Unique.TempVariableName();
            var step = _ctx.Unique.TempVariableName();

            // both ends are settled before the loop starts. The end is read again on every turn, so
            // one that suspends has to be reduced to a name first - the suspension happens once,
            // where the source put it, not once per iteration
            var start = Spill(node.RangeStart, output);
            var end = Spill(node.RangeEnd, output);

            output.Add(Expr.Var(index, start));
            output.Add(Expr.Var(step, Expr.Invoke("Math", "Sign", Expr.Sub(end, Expr.Get(index)))));
            output.Add(new LabelNode(beginLabel));
            output.Add(new GotoNode(endLabel, Expr.Equal(Expr.Get(index), end)));
            output.Add(LoopBody(node, Expr.Get(index)));
            output.Add(Expr.Set(index, Expr.Add(Expr.Get(index), Expr.Get(step))));
            output.Add(new GotoNode(beginLabel));
            output.Add(new LabelNode(endLabel));
        }

        /// <summary>
        /// Wraps a loop body in the frame that holds its iteration variable, exactly as the node's
        /// own expansion does.
        /// </summary>
        private CodeBlockNode LoopBody(ForeachNode node, NodeBase itemGetter)
        {
            NodeBase assignment;

            if (node.Local == null)
            {
                // the statement that declares the iteration variable is invented here, so the name
                // it registers is told where the loop header spells it: an editor has nothing else
                // to connect the header to the uses in the body
                var declaration = Expr.Let(node.VariableName, itemGetter);
                declaration.NameLocation = node.VariableLocation;
                assignment = declaration;
            }
            else
            {
                assignment = Expr.Set(node.Local, itemGetter);
            }

            // the frame is a loop frame, exactly as the node's own expansion makes it: a name
            // declared in a loop is a fresh name on every iteration, and a lambda that captures it
            // must capture that iteration's one
            var wrapper = new CodeBlockNode(ScopeKind.Loop);
            wrapper.Add(assignment);
            wrapper.Add(LowerBlock(node.Body, false));
            CopyLocation(node, wrapper);
            return wrapper;
        }

        #endregion

        #region Resume points

        /// <summary>
        /// Claims the next state number and the label that resumes it, and records which region it
        /// belongs to so that the dispatch can find its way in.
        /// </summary>
        private ResumePoint NewResumePoint()
        {
            var point = new ResumePoint(++_stateId);

            _region.Points.Add(point);
            _region.Register(point.State);

            return point;
        }

        /// <summary>
        /// Runs a suspension, and follows it with the check that decides whether the machine is
        /// being resumed or unwound.
        /// </summary>
        private void Suspend(Action<ResumePoint> emit, List<NodeBase> output)
        {
            emit(NewResumePoint());

            var unwind = _emitUnwind?.Invoke(_region.Unwind, _region != Body);
            if (unwind != null)
                output.Add(unwind);
        }

        private void LowerYield(YieldNode node, List<NodeBase> output)
        {
            if (_emitYield == null)
                Error(node, CompilerMessages.YieldNotInIterator);

            // what is being yielded may itself suspend, and has to have finished doing so before
            // the value is handed over
            var yielded = Spill(node.Expression, output);

            if (!node.IsSequence)
            {
                Suspend(point => _emitYield(yielded, point, output), output);
                return;
            }

            // 'yield from' is a loop that yields each item in turn: once the machine exists it costs
            // nothing beyond the loop, and it is how iterators are composed
            var beginLabel = NewLabel("yieldfrom");
            var endLabel = NewLabel("endyieldfrom");
            var iterator = _ctx.Unique.TempVariableName();

            output.Add(Expr.Var(iterator, new GetEnumeratorNode(yielded)));
            output.Add(new LabelNode(beginLabel));
            output.Add(new GotoNode(endLabel, Expr.Invoke(Expr.Get(iterator), "MoveNext"), false));
            // the item comes out of an enumerator nobody wrote, so it is pointed at the sequence
            // the script did write - an item of the wrong type is a mistake about that expression
            var item = Expr.GetMember(Expr.Get(iterator), "Current");
            CopyLocation(node.Expression, item);

            Suspend(point => _emitYield(item, point, output), output);
            output.Add(new GotoNode(beginLabel));
            output.Add(new LabelNode(endLabel));
            output.Add(new DisposeNode(Expr.Get(iterator)));
        }

        #endregion

        #region Protected regions

        /// <summary>
        /// Rewrites a try so that a resume point inside it can be reached and left again.
        ///
        /// Two IL rules decide the shape. Nothing may branch into a protected region, so the region
        /// gets a dispatch of its own just inside it and the enclosing dispatch only knows how to
        /// reach its entry. And leaving a region runs its finally handlers, which is exactly wrong
        /// for a suspension - so the handler bodies stop being handler bodies: the catch clauses are
        /// reduced to stashing which one fired, and their code, along with the finally, moves out of
        /// the region and runs afterwards. Once out there it is ordinary code, and may suspend as
        /// freely as anything else.
        ///
        ///     var h = 0
        ///     var e : Exception                     # only when there is a finally
        ///     try
        ///         [dispatch]
        ///         try
        ///             [dispatch]
        ///             [body]
        ///         catch (E1 e1) -> h = 1
        ///         catch (E2 e2) -> h = 2
        ///         goto handler_1 if h == 1
        ///         goto handler_2 if h == 2
        ///         goto handled
        ///         handler_1: h = 0; [first catch body];  goto handled
        ///         handler_2: h = 0; [second catch body]; goto handled
        ///         handled:
        ///     catch (Exception e) -> ()             # only when there is a finally
        ///     [finally body]
        ///     goto rethrown if e == null
        ///     rethrow e
        ///     rethrown:
        /// </summary>
        private void LowerTry(TryNode node, List<NodeBase> output)
        {
            var hasFinally = node.Finally != null;
            var handlerVar = node.CatchClauses.Count > 0 ? _ctx.Unique.TempVariableName() : null;
            var pendingVar = hasFinally ? _ctx.Unique.TempVariableName() : null;

            if (handlerVar != null)
                output.Add(Expr.Var(handlerVar, Expr.Int(0)));

            // every name a moved handler reads has to be declared before the try rather than by the
            // catch clause that fills it in: a clause's names come into being while the try is
            // transformed, and by then the statements after it have already been bound
            var caught = DeclareHandlerNames(node, output);

            void Guarded(List<NodeBase> inner) => EmitGuardedBody(node, handlerVar, caught, inner);

            if (!hasFinally)
            {
                Guarded(output);
                return;
            }

            output.Add(Expr.Var(pendingVar, ExceptionType));

            // the finally has to run when a handler body throws as well, so everything above -
            // including the handler bodies that have just been moved out of their own region - goes
            // inside one more region whose only job is to remember the exception
            var statement = Region(Guarded, new[] {StashClause(pendingVar)}, "finally", out var region);

            output.Add(new LabelNode(region.Entry));
            output.Add(statement);

            // a suspension inside the region leaves to here when the machine is being abandoned,
            // which is what makes the finally run for an iterator nobody finished reading
            output.Add(new LabelNode(region.Unwind));
            output.Add(LowerBlock(node.Finally, false));

            var rethrown = NewLabel("rethrown");
            output.Add(new GotoNode(rethrown, Expr.Equal(Expr.Get(pendingVar), Expr.Null())));
            output.Add(Rethrow(pendingVar));
            output.Add(new LabelNode(rethrown));

            ContinueUnwinding(output);
        }

        /// <summary>
        /// Hands the unwinding on to the region around this one, once this one has run whatever it
        /// owed.
        /// </summary>
        private void ContinueUnwinding(List<NodeBase> output)
        {
            var unwind = _emitUnwind?.Invoke(_region.Unwind, _region != Body);
            if (unwind != null)
                output.Add(unwind);
        }

        /// <summary>
        /// Declares the name each catch clause's exception will be read through once its body has
        /// been moved out of the region, and returns them in clause order.
        /// </summary>
        private List<string> DeclareHandlerNames(TryNode node, List<NodeBase> output)
        {
            var result = new List<string>();

            foreach (var curr in node.CatchClauses)
            {
                // a handler that no longer catches anything still needs a name for the exception,
                // because a bare rethrow in its body has to be given something to throw
                var name = string.IsNullOrEmpty(curr.ExceptionVariable)
                    ? _ctx.Unique.TempVariableName()
                    : curr.ExceptionVariable;

                output.Add(Expr.Var(name, curr.ExceptionType ?? ExceptionType));
                result.Add(name);
            }

            return result;
        }

        /// <summary>
        /// The try itself, and the handler bodies that used to be its catch clauses.
        /// </summary>
        private void EmitGuardedBody(TryNode node, string handlerVar, List<string> caught, List<NodeBase> output)
        {
            if (node.CatchClauses.Count == 0)
            {
                output.Add(LowerBlock(node.Code, false));
                return;
            }

            var stash = new List<CatchNode>();
            var handlers = new List<Tuple<CatchNode, string, LabelRef>>();

            foreach (var curr in node.CatchClauses)
            {
                var name = caught[handlers.Count];
                var slot = _ctx.Unique.TempVariableName();

                stash.Add(
                    new CatchNode
                    {
                        ExceptionType = curr.ExceptionType,
                        ExceptionVariable = slot,
                        Code = Expr.Block(
                            Expr.Set(name, Expr.Get(slot)),
                            Expr.Set(handlerVar, Expr.Int(handlers.Count + 1))
                        )
                    }
                );

                handlers.Add(Tuple.Create(curr, name, NewLabel("handler")));
            }

            var statement = Region(inner => inner.Add(LowerBlock(node.Code, false)), stash, "try", out var region);

            output.Add(new LabelNode(region.Entry));
            output.Add(statement);
            output.Add(new LabelNode(region.Unwind));

            var handled = NewLabel("handled");
            for (var idx = 0; idx < handlers.Count; idx++)
                output.Add(new GotoNode(handlers[idx].Item3, Expr.Equal(Expr.Get(handlerVar), Expr.Int(idx + 1))));

            output.Add(new GotoNode(handled));

            foreach (var curr in handlers)
            {
                output.Add(new LabelNode(curr.Item3));
                output.Add(Expr.Set(handlerVar, Expr.Int(0)));
                output.Add(LowerHandlerBody(curr.Item1.Code, curr.Item2));
                output.Add(new GotoNode(handled));
            }

            output.Add(new LabelNode(handled));

            // a try that has no finally owes nothing on the way out, but the region around it may
            if (node.Finally == null)
                ContinueUnwinding(output);
        }

        /// <summary>
        /// Builds a protected region: its own dispatch first, then whatever the caller puts in it.
        /// </summary>
        private TryNode Region(Action<List<NodeBase>> fill, IEnumerable<CatchNode> catches, string name, out LoweredRegion region)
        {
            var outer = _region;
            region = new LoweredRegion(outer, name + "_" + ++_labelId, NewLabel("unwind"));
            _region = region;

            var statements = new List<NodeBase>();
            fill(statements);

            var code = new CodeBlockNode();
            code.AddRange(BuildDispatch(_region));
            code.AddRange(statements);

            _region = outer;

            return new TryNode {Code = code, CatchClauses = catches.ToList()};
        }

        /// <summary>
        /// The catch clause that only remembers what happened, so that the finally can run outside
        /// the region and the exception can be thrown again after it.
        /// </summary>
        private CatchNode StashClause(string pendingVar)
        {
            var slot = _ctx.Unique.TempVariableName();

            return new CatchNode
            {
                ExceptionType = ExceptionType,
                ExceptionVariable = slot,
                Code = Expr.Block(Expr.Set(pendingVar, Expr.Get(slot)))
            };
        }

        /// <summary>
        /// Lowers a handler body, with a bare rethrow rewritten to name the exception explicitly.
        /// </summary>
        private CodeBlockNode LowerHandlerBody(CodeBlockNode body, string variable)
        {
            var previous = _rethrowVariable;
            _rethrowVariable = variable;

            var result = LowerBlock(body, false);

            _rethrowVariable = previous;

            if (ContainsBareRethrow(result))
                Error(body, CompilerMessages.RethrowInMovedHandler);

            return result;
        }

        /// <summary>
        /// Checks for a bare rethrow the pass could not reach - one nested inside a construct that
        /// had no reason to be flattened.
        /// </summary>
        private static bool ContainsBareRethrow(NodeBase node)
        {
            if (node == null || node is CatchNode)
                return false;

            if (node is ThrowNode throwNode && throwNode.Expression == null)
                return true;

            return node.GetChildren().Any(child => ContainsBareRethrow(child?.Node));
        }

        /// <summary>
        /// Throws an exception again without losing where it came from.
        /// </summary>
        private static NodeBase Rethrow(string variable)
        {
            return Expr.Invoke(
                Expr.Invoke("System.Runtime.ExceptionServices.ExceptionDispatchInfo", "Capture", Expr.Get(variable)),
                "Throw"
            );
        }

        /// <summary>
        ///     var r = expr
        ///     try
        ///         var x = r
        ///         [body]
        ///     finally
        ///         r.Dispose ()
        ///
        /// The node's own expansion says the same thing, but it says it while binding - by which
        /// time this pass has long finished.
        /// </summary>
        private void LowerUsing(UsingNode node, List<NodeBase> output)
        {
            var resource = _ctx.Unique.TempVariableName();

            var body = new CodeBlockNode();

            // the name is a variable rather than a constant, because that is what the node's own
            // expansion gives the body and a script is allowed to assign to it
            if (!string.IsNullOrEmpty(node.VariableName))
                body.Add(Expr.Var(node.VariableName, Expr.Get(resource)));

            body.Add(node.Body);

            output.Add(Expr.Var(resource, Spill(node.Expression, output)));

            LowerTry(
                new TryNode
                {
                    Code = body,
                    Finally = Expr.Block(Expr.Invoke(Expr.Get(resource), "Dispose"))
                },
                output
            );
        }

        /// <summary>
        /// Lowers the bodies of a match's cases, leaving the match itself alone.
        ///
        /// A match needs none of the region machinery, because it opens no protected region: the
        /// node already expands into a flat run of labels and jumps, and a dispatch can land in the
        /// middle of one as freely as anywhere else. All that is missing is that the expansion
        /// happens while binding, long after this pass - so the pass reaches into the case bodies
        /// instead, and lets the node expand around them as it always did.
        ///
        /// Arriving in the middle skips the rule checks and the declarations of the names the
        /// pattern bound. That is what should happen: inside a machine those names are fields, and
        /// they still hold what the check that matched put there.
        ///
        /// A match whose value is wanted is given the name to put it in, and the case bodies assign
        /// to it rather than producing it - what one case leaves on the evaluation stack is not
        /// there when a resuming machine arrives at the label after them.
        /// </summary>
        private void LowerMatch(MatchNode node, List<NodeBase> output, string resultName = null)
        {
            // the value being matched is settled before any rule is tried, so a suspension in it is
            // one that happens before the match
            var matched = Spill(node.Expression, output);

            // a lambda inside a match belongs to the frame the match opens, and a machine that
            // resumes into a case body arrives after that frame was set up
            if (ContainsAnywhere<LambdaNode>(node))
                Error(node, CompilerMessages.LambdaInMatchedResumePoint);

            var statements = new List<MatchStatementNode>();

            foreach (var curr in node.MatchStatements)
            {
                statements.Add(
                    new MatchStatementNode
                    {
                        MatchRules = curr.MatchRules,
                        Condition = LowerGuard(curr.Condition),
                        Expression = LowerCaseBody(curr.Expression, resultName)
                    }
                );
            }

            var result = new MatchNode {Expression = matched, MatchStatements = statements};
            CopyLocation(node, result);
            output.Add(result);
        }

        /// <summary>
        /// A guard is asked only once its rules have matched, and the match asks it as an
        /// expression - so a suspension in one becomes a block whose statements suspend and whose
        /// value is the answer. That keeps the suspension behind the same check the guard is, which
        /// a statement lifted out in front of the match would not.
        /// </summary>
        private NodeBase LowerGuard(NodeBase condition)
        {
            if (condition == null || !ContainsResumePoint(condition))
                return condition;

            var statements = new List<NodeBase>();
            var spilled = Spill(condition, statements);
            statements.Add(spilled);

            var result = new CodeBlockNode();
            result.AddRange(statements);
            CopyLocation(condition, result);
            return result;
        }

        private NodeBase LowerCaseBody(NodeBase body, string resultName)
        {
            if (resultName != null)
            {
                body = body is CodeBlockNode valued
                    ? AssignTail(valued, resultName)
                    : AssignValue(body, resultName);
            }

            if (body is CodeBlockNode block)
                return LowerBlock(block, false);

            // a case body written as a single expression is still a statement's worth of lowering:
            // it may suspend, and then it is several
            var statements = new List<NodeBase>();
            LowerStatement(body, false, statements);

            if (statements.Count == 1)
                return statements[0];

            var wrapper = new CodeBlockNode();
            wrapper.AddRange(statements);
            CopyLocation(body, wrapper);
            return wrapper;
        }

        /// <summary>
        /// The statements that send a resuming machine into this region: straight to the point it
        /// stopped at when that point is here, or to the entry of the nested region that holds it.
        /// </summary>
        private static List<NodeBase> BuildDispatch(LoweredRegion region)
        {
            var result = new List<NodeBase>();

            foreach (var child in region.Children)
            {
                if (!child.HasStates)
                    continue;

                result.Add(
                    new GotoNode(
                        child.Entry,
                        Expr.And(
                            Expr.GreaterEqual(State(), Expr.Int(child.FirstState)),
                            Expr.LessEqual(State(), Expr.Int(child.LastState))
                        )
                    )
                );
            }

            foreach (var curr in region.Points)
                result.Add(new GotoNode(curr.Label, Expr.Equal(State(), Expr.Int(curr.State))));

            return result;
        }

        private static NodeBase State()
        {
            return Expr.GetMember(Expr.This(), EntityNames.StateFieldName);
        }

        private static readonly TypeSignature ExceptionType = new TypeSignature("System.Exception");

        #endregion

        #region Helpers

        internal static void CopyLocation(LocationEntity from, LocationEntity to)
        {
            to.StartLocation = from.StartLocation;
            to.EndLocation = from.EndLocation;
        }

        private LabelRef NewLabel(string kind)
        {
            return new LabelRef($"{kind}_{++_labelId}");
        }

        private static void Error(NodeBase node, string message, params object[] args)
        {
            throw new LensCompilerException(string.Format(message, args), node);
        }

        /// <summary>
        /// Checks whether a subtree suspends the function currently being rewritten. A lambda's
        /// yields and awaits are its own, not this function's, and are rejected separately.
        /// </summary>
        public static bool ContainsResumePoint(NodeBase node)
        {
            if (node == null || node is FunctionNodeBase)
                return false;

            if (node is YieldNode || node is AwaitNode)
                return true;

            return node.GetChildren().Any(child => ContainsResumePoint(child?.Node));
        }

        /// <summary>
        /// Checks whether a subtree contains a node of a kind anywhere at all, lambdas included.
        /// </summary>
        public static bool ContainsAnywhere<T>(NodeBase node)
            where T : NodeBase
        {
            return FindFirst<T>(node) != null;
        }

        /// <summary>
        /// Returns the first node of a kind in a subtree, lambdas included, or null when there is
        /// none. This is what lets a problem with a resume point be reported where the resume point
        /// is, rather than at the top of whatever contains it.
        /// </summary>
        public static T FindFirst<T>(NodeBase node)
            where T : NodeBase
        {
            if (node == null)
                return null;

            if (node is T match)
                return match;

            foreach (var child in node.GetChildren())
            {
                var found = FindFirst<T>(child?.Node);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Reports a resume point that appears where no state machine can consume it.
        /// </summary>
        public static void CheckNoNestedResumePoints(NodeBase node)
        {
            if (node == null)
                return;

            if (node is FunctionNodeBase fn)
            {
                if (ContainsAnywhere<YieldNode>(fn.Body) || ContainsAnywhere<AwaitNode>(fn.Body))
                    Error(node, CompilerMessages.ResumePointInLambda);

                return;
            }

            foreach (var child in node.GetChildren())
                CheckNoNestedResumePoints(child?.Node);
        }

        #endregion
    }
}
