using Lens.Analysis;
using Lens.SyntaxTree;

namespace Lens.LanguageServer.Core
{
    /// <summary>
    /// A position in a document, counted the way editors count: from zero.
    ///
    /// The compiler counts from one. That difference is converted here and nowhere else, which is
    /// the only way to keep it from turning into off-by-one bugs scattered across features.
    /// </summary>
    public struct TextPosition
    {
        public TextPosition(int line, int character)
        {
            Line = line;
            Character = character;
        }

        /// <summary>
        /// Zero-based line.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Zero-based character within the line.
        /// </summary>
        public int Character { get; }

        /// <summary>
        /// The same position as the compiler would write it.
        /// </summary>
        public LexemLocation ToLocation()
        {
            return new LexemLocation {Line = Line + 1, Offset = Character + 1};
        }

        /// <summary>
        /// An editor position from a compiler one.
        /// </summary>
        public static TextPosition FromLocation(LexemLocation location)
        {
            return new TextPosition(
                location.Line > 0 ? location.Line - 1 : 0,
                location.Offset > 0 ? location.Offset - 1 : 0
            );
        }

        public override string ToString() => $"{Line}:{Character}";
    }

    /// <summary>
    /// A stretch of a document, in editor coordinates.
    /// </summary>
    public struct TextRange
    {
        public TextRange(TextPosition start, TextPosition end)
        {
            Start = start;
            End = end;
        }

        public TextPosition Start { get; }
        public TextPosition End { get; }

        /// <summary>
        /// The editor's view of a compiler span.
        ///
        /// A range that ends before it starts is not something an editor will accept, and a node
        /// the parser could not finish reading can produce one. It collapses to its start instead.
        /// </summary>
        public static TextRange FromSpan(TextSpan span)
        {
            var start = TextPosition.FromLocation(span.Start);
            var end = TextPosition.FromLocation(span.End);

            return new TextRange(start, Precedes(end, start) ? start : end);
        }

        /// <summary>
        /// The smallest range holding both of these, for the places a protocol requires one range
        /// to contain another.
        /// </summary>
        public static TextRange Union(TextRange left, TextRange right)
        {
            return new TextRange(
                Precedes(right.Start, left.Start) ? right.Start : left.Start,
                Precedes(left.End, right.End) ? right.End : left.End
            );
        }

        /// <summary>
        /// Whether one position comes strictly before another.
        /// </summary>
        public static bool Precedes(TextPosition left, TextPosition right)
        {
            return left.Line < right.Line || (left.Line == right.Line && left.Character < right.Character);
        }

        public override string ToString() => $"{Start}-{End}";
    }
}
