using System.Collections.Generic;
using Lens.SyntaxTree;

namespace Lens.Analysis
{
    /// <summary>
    /// A stretch of source code.
    ///
    /// Positions are the compiler's own: lines and offsets both count from one. An editor that
    /// counts from zero converts on the way in and out, which is the only place the difference
    /// belongs.
    /// </summary>
    public struct TextSpan
    {
        public TextSpan(LexemLocation start, LexemLocation end)
        {
            Start = start;
            End = end;
        }

        /// <summary>
        /// The first character of the span.
        /// </summary>
        public LexemLocation Start { get; }

        /// <summary>
        /// One past the last character of the span.
        /// </summary>
        public LexemLocation End { get; }

        /// <summary>
        /// Whether the span points anywhere at all.
        /// </summary>
        public bool IsEmpty => Start.Line == 0 && End.Line == 0;

        public override string ToString()
        {
            return $"{Start}-{End}";
        }
    }

    /// <summary>
    /// What a token is, for the purpose of colouring it.
    ///
    /// Most of this comes straight from the lexer, which already classifies every lexem. The
    /// entries that do not - a name that turns out to be a type, a function or a variable - are
    /// what an editor cannot work out for itself, and are the reason semantic colouring beats a
    /// regular expression.
    /// </summary>
    public enum TokenKind
    {
        Keyword,
        Identifier,
        Type,
        Function,
        Variable,
        Parameter,
        Field,
        Number,
        String,
        Regex,
        Operator
    }

    /// <summary>
    /// What a name refers to.
    /// </summary>
    public enum SymbolKind
    {
        Local,
        Parameter,
        Function,
        Record,
        RecordField,
        AlgebraicType,
        TypeLabel,
        HostType,
        GlobalVariable,
        Member,
        Keyword,
        Namespace
    }

    /// <summary>
    /// One coloured token.
    /// </summary>
    public sealed class ClassifiedToken
    {
        internal ClassifiedToken(TextSpan span, TokenKind kind, string text)
        {
            Span = span;
            Kind = kind;
            Text = text;
        }

        public TextSpan Span { get; }
        public TokenKind Kind { get; }
        public string Text { get; }

        public override string ToString() => $"{Kind}({Text}) at {Span}";
    }

    /// <summary>
    /// A problem found in the script.
    /// </summary>
    public sealed class AnalysisDiagnostic
    {
        internal AnalysisDiagnostic(string message, bool isError, TextSpan span)
        {
            Message = message;
            IsError = isError;
            Span = span;
        }

        public string Message { get; }
        public bool IsError { get; }
        public TextSpan Span { get; }

        public override string ToString() => $"{(IsError ? "error" : "warning")} {Span}: {Message}";
    }

    /// <summary>
    /// A name the script can use at some point in the file.
    /// </summary>
    public sealed class CompletionEntry
    {
        internal CompletionEntry(string label, SymbolKind kind, string detail)
        {
            Label = label;
            Kind = kind;
            Detail = detail;
        }

        /// <summary>
        /// The text to insert and to show.
        /// </summary>
        public string Label { get; }

        public SymbolKind Kind { get; }

        /// <summary>
        /// The signature or type, shown beside the label.
        /// </summary>
        public string Detail { get; }

        public override string ToString() => $"{Label}: {Detail}";
    }

    /// <summary>
    /// A name, everywhere it is written, and whether it may be renamed.
    /// </summary>
    public sealed class ScriptSymbol
    {
        internal ScriptSymbol(string name, SymbolKind kind, string detail, TextSpan? declaration, IReadOnlyList<TextSpan> references, bool canRename, string renameRefusal = null)
        {
            Name = name;
            Kind = kind;
            Detail = detail;
            Declaration = declaration;
            References = references;
            CanRename = canRename;
            RenameRefusal = renameRefusal;
        }

        public string Name { get; }
        public SymbolKind Kind { get; }

        /// <summary>
        /// The type or signature, for hover.
        /// </summary>
        public string Detail { get; }

        /// <summary>
        /// Where the name was declared, when the script is what declares it.
        /// </summary>
        public TextSpan? Declaration { get; }

        /// <summary>
        /// Every place the name is written, the declaration included.
        /// </summary>
        public IReadOnlyList<TextSpan> References { get; }

        /// <summary>
        /// Whether renaming would be correct. False for anything the script does not own - a host
        /// type, a host function, a member of a .NET type - since renaming those would only break
        /// the reference.
        /// </summary>
        public bool CanRename { get; }

        /// <summary>
        /// Why the rename was refused, when it was.
        /// </summary>
        public string RenameRefusal { get; }

        public override string ToString() => $"{Kind} {Name}";
    }

    /// <summary>
    /// An assembly a 'declare reference' line asks for.
    ///
    /// The compiler ignores these - the host chooses its own assemblies - so checking that the file
    /// is actually there is a job for tooling, and this is what tooling needs to do it.
    /// </summary>
    public sealed class ReferencedAssembly
    {
        internal ReferencedAssembly(string path, TextSpan span)
        {
            Path = path;
            Span = span;
        }

        /// <summary>
        /// The path as written: absolute, or relative to the script.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Where it is written, so that a warning can point at it.
        /// </summary>
        public TextSpan Span { get; }

        public override string ToString() => Path;
    }

    /// <summary>
    /// An entry in the file's outline.
    /// </summary>
    public sealed class OutlineItem
    {
        internal OutlineItem(string name, SymbolKind kind, string detail, TextSpan span, TextSpan selection, IReadOnlyList<OutlineItem> children)
        {
            Name = name;
            Kind = kind;
            Detail = detail;
            Span = span;
            Selection = selection;
            Children = children;
        }

        public string Name { get; }
        public SymbolKind Kind { get; }
        public string Detail { get; }

        /// <summary>
        /// The whole declaration.
        /// </summary>
        public TextSpan Span { get; }

        /// <summary>
        /// The name within it, which is what an editor highlights when the entry is selected.
        /// </summary>
        public TextSpan Selection { get; }

        public IReadOnlyList<OutlineItem> Children { get; }

        public override string ToString() => $"{Kind} {Name}";
    }
}
