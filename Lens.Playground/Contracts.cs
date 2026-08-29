using System.Collections.Generic;

namespace Lens.Playground
{
    /// <summary>
    /// The shapes the page and the compiler exchange.
    ///
    /// They are plain property bags on purpose: everything here crosses into JavaScript, so
    /// nothing may carry behaviour, and the editor's own vocabulary (zero-based lines, flat token
    /// arrays) wins over the compiler's wherever the two disagree.
    /// </summary>
    public sealed class RangeDto
    {
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int EndLine { get; set; }
        public int EndColumn { get; set; }
    }

    /// <summary>
    /// One entry of the problem list, as the editor's marker model wants it.
    /// </summary>
    public sealed class ProblemDto
    {
        public string Message { get; set; }

        /// <summary>
        /// "error" or "warning".
        /// </summary>
        public string Severity { get; set; }

        public RangeDto Range { get; set; }
    }

    /// <summary>
    /// One name offered in the completion list.
    /// </summary>
    public sealed class SuggestionDto
    {
        public string Label { get; set; }

        /// <summary>
        /// The symbol kind as named by <see cref="Lens.Analysis.SymbolKind"/>, which the page maps
        /// to one of the editor's icons.
        /// </summary>
        public string Kind { get; set; }

        public string Detail { get; set; }
    }

    /// <summary>
    /// What to show when the pointer rests on a name.
    /// </summary>
    public sealed class ExplanationDto
    {
        public string Text { get; set; }
        public RangeDto Range { get; set; }
    }

    /// <summary>
    /// An entry of the outline, which nests.
    /// </summary>
    public sealed class OutlineDto
    {
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Detail { get; set; }
        public RangeDto Range { get; set; }
        public RangeDto Selection { get; set; }
        public List<OutlineDto> Children { get; set; }
    }

    /// <summary>
    /// A rename that was allowed, or the reason it was not.
    /// </summary>
    public sealed class RenameDto
    {
        public bool IsAllowed { get; set; }
        public string Refusal { get; set; }
        public List<EditDto> Edits { get; set; }
    }

    /// <summary>
    /// One replacement of a rename.
    /// </summary>
    public sealed class EditDto
    {
        public RangeDto Range { get; set; }
        public string Text { get; set; }
    }

    /// <summary>
    /// The outcome of running a script.
    ///
    /// Output and the result are separate: a script both prints and evaluates to something, and
    /// the pane shows the two differently.
    /// </summary>
    public sealed class RunResultDto
    {
        /// <summary>
        /// Everything the script wrote to the console.
        /// </summary>
        public string Output { get; set; }

        /// <summary>
        /// The rendered value the script evaluated to, absent when it failed.
        /// </summary>
        public string Result { get; set; }

        /// <summary>
        /// The type of that value, for the dimmed line under it.
        /// </summary>
        public string ResultType { get; set; }

        /// <summary>
        /// What went wrong, absent when nothing did.
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// Where it went wrong, when the failure was the compiler's and it knew a place.
        /// </summary>
        public RangeDto ErrorRange { get; set; }

        /// <summary>
        /// Whether the error came from the compiler rather than from the running script.
        /// </summary>
        public bool IsCompileError { get; set; }

        /// <summary>
        /// Wall-clock time of compiling and running, in milliseconds.
        /// </summary>
        public double ElapsedMs { get; set; }
    }

    /// <summary>
    /// A script offered in the samples menu.
    /// </summary>
    public sealed class SampleDto
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Source { get; set; }
    }
}
