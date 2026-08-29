using Lens.SyntaxTree;

namespace Lens.Lexer
{
    /// <summary>
    /// A single segment of an interpolated string literal.
    /// It is either a literal chunk of text or a hole containing an expression.
    /// </summary>
    internal class InterpolatedStringPart
    {
        #region Constructors

        /// <summary>
        /// Creates a literal chunk.
        /// </summary>
        public static InterpolatedStringPart FromLiteral(string literal)
        {
            return new InterpolatedStringPart {Literal = literal};
        }

        /// <summary>
        /// Creates a hole.
        /// </summary>
        public static InterpolatedStringPart FromHole(string expression, string format, LexemLocation location)
        {
            return new InterpolatedStringPart
            {
                Expression = expression,
                Format = format,
                StartLocation = location
            };
        }

        #endregion

        #region Fields

        /// <summary>
        /// Text of a literal chunk, with all escape sequences already resolved.
        /// Null for holes.
        /// </summary>
        public string Literal;

        /// <summary>
        /// Raw source code of a hole's expression.
        /// Null for literal chunks.
        /// </summary>
        public string Expression;

        /// <summary>
        /// Optional format specifier of a hole (the part after the colon).
        /// </summary>
        public string Format;

        /// <summary>
        /// Location of the hole's expression within the outer source code.
        /// Used to shift the locations reported by the nested lexer and parser.
        /// </summary>
        public LexemLocation StartLocation;

        /// <summary>
        /// Checks if the current part is a hole rather than a literal chunk.
        /// </summary>
        public bool IsHole => Expression != null;

        #endregion

        #region Methods

        /// <summary>
        /// Shifts a location produced by a nested lexer or parser to its real position in the outer source.
        /// </summary>
        public static LexemLocation Shift(LexemLocation location, LexemLocation origin)
        {
            if (location.Line == 0)
                return location;

            return new LexemLocation
            {
                Line = origin.Line + location.Line - 1,

                // only the first line of a hole is horizontally offset by the hole's own position
                Offset = location.Line == 1
                    ? origin.Offset + location.Offset - 1
                    : location.Offset
            };
        }

        /// <summary>
        /// Shifts a location produced by a nested lexer or parser of this hole to its real position
        /// in the outer source.
        /// </summary>
        public LexemLocation Shift(LexemLocation location)
        {
            return Shift(location, StartLocation);
        }

        #endregion
    }
}
