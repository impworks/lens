namespace Lens.SyntaxTree
{
    /// <summary>
    /// The position of a caret in the text.
    /// </summary>
    public struct LexemLocation
    {
        #region Fields

        /// <summary>
        /// The 0-based number of line in current file.
        /// </summary>
        public int Line;

        /// <summary>
        /// The 0-based position of the character in current line.
        /// </summary>
        public int Offset;

        #endregion

        public override string ToString()
        {
            return $"{Line}:{Offset}";
        }
    }
}