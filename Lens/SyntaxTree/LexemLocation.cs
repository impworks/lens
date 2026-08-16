namespace Lens.SyntaxTree
{
    /// <summary>
    /// The position of a caret in the text.
    ///
    /// Both coordinates are 1-based, which is also what makes 0:0 usable as the absence of a
    /// position: a diagnostic that carries one has nowhere in the source to point at.
    /// </summary>
    public struct LexemLocation
    {
        #region Fields

        /// <summary>
        /// The 1-based number of line in current file.
        /// </summary>
        public int Line;

        /// <summary>
        /// The 1-based position of the character in current line.
        /// </summary>
        public int Offset;

        #endregion

        public override string ToString()
        {
            return $"{Line}:{Offset}";
        }
    }
}