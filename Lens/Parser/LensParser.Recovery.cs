using System;
using System.Collections.Generic;
using Lens.Lexer;
using Lens.SyntaxTree;
using Lens.SyntaxTree.Internals;
using Lens.Translations;

namespace Lens.Parser
{
    /// <summary>
    /// The half of the parser that keeps going after a mistake.
    ///
    /// A compiler wants to stop at the first problem; an editor wants a tree for the 95% of the file
    /// that is fine, because a file being typed into is malformed most of the time. Both are served
    /// by one grammar here - recovery is a mode, not a second parser.
    ///
    /// Recovery happens at statement boundaries only, which the language makes cheap: statements are
    /// delimited by newlines and blocks by indentation, so "skip to the next statement at this
    /// nesting level" is a well-defined operation on the lexem stream.
    ///
    /// Note that Attempt() does not catch exceptions, so no speculative decision the parser makes
    /// has ever depended on one propagating. Turning a throw into a recovery here therefore cannot
    /// change which alternative the parser picks - it only changes how much of the file survives.
    /// </summary>
    internal partial class LensParser
    {
        #region Recovering entry points

        /// <summary>
        /// main, recovering: every statement that fails is recorded and skipped, and the next one
        /// is read as though nothing had happened.
        /// </summary>
        private List<NodeBase> ParseMainTolerant()
        {
            var result = new List<NodeBase>();

            while (!Peek(LexemType.Eof))
            {
                var start = _lexemId;

                if (TryParse(ParseStmt, out var stmt) && stmt != null)
                {
                    result.Add(stmt);

                    if (Peek(LexemType.Eof))
                        break;

                    if (IsStmtSeparator())
                        continue;

                    Failures.Add(MakeError(ParserMessages.NewlineSeparatorExpected));
                }

                SkipToNextStatement(start);
            }

            return result;
        }

        /// <summary>
        /// local_stmt_list, recovering.
        ///
        /// Always yields at least one statement, an <see cref="ErrorNode"/> if nothing else, so that
        /// a block is never empty - the constructs that own one all assume a body.
        /// </summary>
        private List<NodeBase> ParseLocalStmtListTolerant()
        {
            var result = new List<NodeBase>();

            if (!Check(LexemType.Indent))
                return result;

            while (!Check(LexemType.Dedent) && !Peek(LexemType.Eof))
            {
                var start = _lexemId;
                var parsed = TryParse(ParseLocalStmt, out var stmt);

                if (parsed && stmt != null)
                {
                    result.Add(stmt);

                    if (Peek(LexemType.Dedent) || Peek(LexemType.Eof) || IsStmtSeparator())
                        continue;

                    // the statement itself was fine; what follows it on the line is not
                    Failures.Add(MakeError(ParserMessages.NewlineSeparatorExpected));
                    start = _lexemId;
                }
                else if (parsed)
                {
                    Failures.Add(MakeError(ParserMessages.ExpressionExpected));
                }

                var recoveryStart = _lexemId;
                SkipToNextStatement(start, stopAtDedent: true);
                result.Add(Placeholder(Math.Min(start, recoveryStart)));
            }

            if (result.Count == 0)
                result.Add(Placeholder(_lexemId));

            return result;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Runs a parsing step, recording rather than propagating whatever it reports.
        /// </summary>
        private bool TryParse<T>(Func<T> getter, out T result)
            where T : NodeBase
        {
            try
            {
                result = Bind(getter);
                return true;
            }
            catch (LensCompilerException ex)
            {
                Failures.Add(ex);
                result = null;
                return false;
            }
            catch (Exception ex)
            {
                // as in the lexer: a mistake of ours, on a path only an editor takes. The statement
                // is dropped and the rest of the file is still read.
                Failures.Add(new LensCompilerException(ex.Message, ex));
                result = null;
                return false;
            }
        }

        /// <summary>
        /// Builds a placeholder covering the lexems that were skipped.
        /// </summary>
        private ErrorNode Placeholder(int start)
        {
            var from = _lexems[Math.Min(start, _lexems.Length - 1)];
            var to = _lexems[Math.Max(Math.Min(_lexemId, _lexems.Length - 1) - 1, 0)];

            return new ErrorNode
            {
                StartLocation = from.StartLocation,
                EndLocation = to.EndLocation
            };
        }

        /// <summary>
        /// Advances past the statement that failed, to wherever the next one begins.
        ///
        /// Nested blocks are skipped whole: a mistake inside a lambda body is not a reason to try to
        /// resume in the middle of it. Progress past <paramref name="start"/> is guaranteed, so the
        /// caller's loop always terminates.
        /// </summary>
        private void SkipToNextStatement(int start, bool stopAtDedent = false)
        {
            if (_lexemId == start)
                Skip();

            var depth = 0;

            while (!Peek(LexemType.Eof))
            {
                var type = _lexems[_lexemId].Type;

                if (type == LexemType.Indent)
                {
                    depth++;
                }
                else if (type == LexemType.Dedent)
                {
                    if (depth > 0)
                    {
                        depth--;
                    }
                    else
                    {
                        // the block this statement lived in has ended, which separates statements
                        // just as a newline does - but the caller's loop wants to see the dedent
                        if (!stopAtDedent)
                            Skip();

                        return;
                    }
                }
                else if (type == LexemType.NewLine && depth == 0)
                {
                    Skip();
                    return;
                }

                Skip();
            }
        }

        /// <summary>
        /// Builds an error bound to the current lexem, without throwing it.
        /// </summary>
        private LensCompilerException MakeError(string msg, params object[] args)
        {
            return new LensCompilerException(
                string.Format(msg, args),
                _lexems[_lexemId]
            );
        }

        #endregion
    }
}
