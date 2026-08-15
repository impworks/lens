using System.Collections.Generic;
using Lens.Analysis;

namespace Lens.LanguageServer.Core
{
    /// <summary>
    /// How serious a problem is.
    /// </summary>
    public enum ProblemSeverity
    {
        Error,
        Warning
    }

    /// <summary>
    /// A problem to show in the document.
    /// </summary>
    public sealed class Problem
    {
        public Problem(string message, ProblemSeverity severity, TextRange range)
        {
            Message = message;
            Severity = severity;
            Range = range;
        }

        public string Message { get; }
        public ProblemSeverity Severity { get; }
        public TextRange Range { get; }

        public override string ToString() => $"{Severity} {Range}: {Message}";
    }

    /// <summary>
    /// A name to offer.
    /// </summary>
    public sealed class Suggestion
    {
        public Suggestion(string label, SymbolKind kind, string detail)
        {
            Label = label;
            Kind = kind;
            Detail = detail;
        }

        public string Label { get; }
        public SymbolKind Kind { get; }
        public string Detail { get; }

        public override string ToString() => Label;
    }

    /// <summary>
    /// What to show when the pointer rests somewhere.
    /// </summary>
    public sealed class Explanation
    {
        public Explanation(string text, TextRange range)
        {
            Text = text;
            Range = range;
        }

        public string Text { get; }
        public TextRange Range { get; }
    }

    /// <summary>
    /// A place in a document.
    /// </summary>
    public sealed class DocumentLocation
    {
        public DocumentLocation(string uri, TextRange range)
        {
            Uri = uri;
            Range = range;
        }

        public string Uri { get; }
        public TextRange Range { get; }

        public override string ToString() => $"{Uri}{Range}";
    }

    /// <summary>
    /// A replacement to make in a document.
    /// </summary>
    public sealed class DocumentEdit
    {
        public DocumentEdit(string uri, TextRange range, string text)
        {
            Uri = uri;
            Range = range;
            Text = text;
        }

        public string Uri { get; }
        public TextRange Range { get; }
        public string Text { get; }
    }

    /// <summary>
    /// The outcome of asking to rename something: either the edits, or why not.
    /// </summary>
    public sealed class RenameOutcome
    {
        private RenameOutcome(IReadOnlyList<DocumentEdit> edits, string refusal)
        {
            Edits = edits;
            Refusal = refusal;
        }

        public IReadOnlyList<DocumentEdit> Edits { get; }

        /// <summary>
        /// Why the rename cannot be done, or null when it can.
        /// </summary>
        public string Refusal { get; }

        public bool IsAllowed => Refusal == null;

        public static RenameOutcome Allowed(IReadOnlyList<DocumentEdit> edits) => new RenameOutcome(edits, null);
        public static RenameOutcome Refused(string reason) => new RenameOutcome(new DocumentEdit[0], reason);
    }

    /// <summary>
    /// A declaration in the file's outline.
    /// </summary>
    public sealed class OutlineEntry
    {
        public OutlineEntry(string name, SymbolKind kind, string detail, TextRange range, TextRange selection, IReadOnlyList<OutlineEntry> children)
        {
            Name = name;
            Kind = kind;
            Detail = detail;
            Range = range;
            Selection = selection;
            Children = children;
        }

        public string Name { get; }
        public SymbolKind Kind { get; }
        public string Detail { get; }
        public TextRange Range { get; }
        public TextRange Selection { get; }
        public IReadOnlyList<OutlineEntry> Children { get; }
    }

    /// <summary>
    /// One coloured run, always within a single line.
    ///
    /// Every protocol that carries semantic colouring wants single-line runs, so the splitting
    /// happens here rather than in each of them.
    /// </summary>
    public sealed class ColouredRun
    {
        public ColouredRun(int line, int character, int length, int tokenType)
        {
            Line = line;
            Character = character;
            Length = length;
            TokenType = tokenType;
        }

        public int Line { get; }
        public int Character { get; }
        public int Length { get; }

        /// <summary>
        /// An index into <see cref="SemanticTokenLegend.TokenTypes"/>.
        /// </summary>
        public int TokenType { get; }
    }
}
