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
    }
}
