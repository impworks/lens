using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace Lens.VisualStudio
{
    /// <summary>
    /// Puts a command filter on every LENS editor window so that hovering over a variable while the
    /// debugger is stopped shows its value.
    ///
    /// A debugger data tip is not part of the language server protocol and is not asked of the
    /// content type, the language client or anything else this extension registers declaratively:
    /// the editor asks the command filter chain of the view, and shows nothing at all if no filter
    /// answers. Which is why a language that debugs perfectly well - breakpoints, stepping and the
    /// Locals window all work off the debug engine and the PDB, never off the editor - can still
    /// have no data tips until somebody hangs this one interface off the view.
    /// </summary>
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(LensContentDefinition.ContentTypeName)]
    [TextViewRole(PredefinedTextViewRoles.Editable)]
    [Name("LENS data tips")]
    internal sealed class LensDataTipFilterProvider : IVsTextViewCreationListener
    {
        [Import]
        internal IVsEditorAdaptersFactoryService AdapterFactory = null;

        public void VsTextViewCreated(IVsTextView adapter)
        {
            var view = AdapterFactory.GetWpfTextView(adapter);

            if (view == null || view.Properties.ContainsProperty(typeof(LensDataTipFilter)))
                return;

            view.Properties.AddProperty(typeof(LensDataTipFilter), new LensDataTipFilter(adapter, view, AdapterFactory));
        }
    }

    /// <summary>
    /// Answers the editor's request for the text of a debugger data tip.
    /// </summary>
    internal sealed class LensDataTipFilter : IOleCommandTarget, IVsTextViewFilter
    {
        private readonly ITextView _view;
        private readonly IVsEditorAdaptersFactoryService _adapterFactory;
        private readonly IOleCommandTarget _next;

        public LensDataTipFilter(IVsTextView adapter, ITextView view, IVsEditorAdaptersFactoryService adapterFactory)
        {
            _view = view;
            _adapterFactory = adapterFactory;

            ErrorHandler.ThrowOnFailure(adapter.AddCommandFilter(this, out _next));
        }

        public int GetDataTipText(TextSpan[] pSpan, out string pbstrText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            pbstrText = null;

            if (pSpan == null || pSpan.Length != 1)
                return VSConstants.E_INVALIDARG;

            if (!(Package.GetGlobalService(typeof(SVsShellDebugger)) is IVsDebugger debugger))
                return VSConstants.E_FAIL;

            // in design mode there is no frame to evaluate against, and the ordinary hover tooltip
            // from the language server is the right thing to show instead
            var mode = new DBGMODE[1];
            if (ErrorHandler.Failed(debugger.GetMode(mode)) || mode[0] == DBGMODE.DBGMODE_Design)
                return VSConstants.E_FAIL;

            var snapshot = _view.TextBuffer.CurrentSnapshot;

            if (!TryGetExpression(snapshot, pSpan[0], out var expression))
                return VSConstants.E_FAIL;

            // the editor only offers the word it guessed at; the span written back here is the one
            // it will underline and anchor the tip to
            pSpan[0] = ToTextSpan(expression);

            var buffer = _adapterFactory.GetBufferAdapter(_view.TextBuffer) as IVsTextLines;

            if (buffer == null)
                return VSConstants.E_FAIL;

            // the return value carries meaning - TIP_S_NODEFAULTTIP tells the editor to show this
            // text and nothing else - so it is passed on untouched
            return debugger.GetDataTipValue(buffer, pSpan, expression.GetText(), out pbstrText);
        }

        public int GetPairExtents(int iLine, int iIndex, TextSpan[] pSpan)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int GetWordExtent(int iLine, int iIndex, uint dwFlags, TextSpan[] pSpan)
        {
            return VSConstants.E_NOTIMPL;
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return _next == null
                ? (int) Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED
                : _next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return _next == null
                ? (int) Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED
                : _next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        /// <summary>
        /// Widens whatever the editor pointed at into the whole expression the user meant.
        ///
        /// The expression is scanned out of the buffer rather than asked of the language server:
        /// this runs on the UI thread while the debugger waits, and a round trip to another process
        /// is not something to do there. A name and the member accesses around it cover what a data
        /// tip is used for; anything more elaborate is typed into the watch window instead.
        /// </summary>
        private static bool TryGetExpression(ITextSnapshot snapshot, TextSpan hovered, out SnapshotSpan expression)
        {
            expression = default(SnapshotSpan);

            if (hovered.iStartLine < 0 || hovered.iStartLine >= snapshot.LineCount)
                return false;

            var line = snapshot.GetLineFromLineNumber(hovered.iStartLine);

            if (hovered.iStartIndex < 0 || hovered.iStartIndex > line.Length)
                return false;

            // a selection is what the user asked for, and is used as it stands
            if (hovered.iEndLine == hovered.iStartLine && hovered.iEndIndex > hovered.iStartIndex)
            {
                expression = new SnapshotSpan(
                    line.Start + hovered.iStartIndex,
                    hovered.iEndIndex - hovered.iStartIndex
                );

                return true;
            }

            var text = line.GetText();
            var at = hovered.iStartIndex;

            // the point can sit just past the last character of the name it belongs to
            if (at == text.Length || !IsNamePart(text[at]))
            {
                if (at == 0 || !IsNamePart(text[at - 1]))
                    return false;

                at--;
            }

            var start = at;
            while (start > 0 && IsNamePart(text[start - 1]))
                start--;

            var end = at;
            while (end < text.Length - 1 && IsNamePart(text[end + 1]))
                end++;

            // walk back over any receivers, so that hovering the last name of a.b.c evaluates the
            // whole chain rather than a member of nothing
            while (start > 0 && text[start - 1] == '.')
            {
                var receiver = start - 1;

                if (receiver == 0 || !IsNamePart(text[receiver - 1]))
                    break;

                while (receiver > 0 && IsNamePart(text[receiver - 1]))
                    receiver--;

                start = receiver;
            }

            if (!IsNameStart(text[start]))
                return false;

            expression = new SnapshotSpan(line.Start + start, end - start + 1);

            return true;
        }

        private static TextSpan ToTextSpan(SnapshotSpan span)
        {
            var start = span.Snapshot.GetLineFromPosition(span.Start);
            var end = span.Snapshot.GetLineFromPosition(span.End);

            return new TextSpan
            {
                iStartLine = start.LineNumber,
                iStartIndex = span.Start - start.Start,
                iEndLine = end.LineNumber,
                iEndIndex = span.End - end.Start
            };
        }

        private static bool IsNamePart(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static bool IsNameStart(char value)
        {
            return char.IsLetter(value) || value == '_';
        }
    }
}
