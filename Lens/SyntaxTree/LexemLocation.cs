namespace Lens.SyntaxTree
{
	/// <summary>
	/// The position of a caret in the text.
	/// </summary>
	public struct LexemLocation
	{
		public int Line;
		public int Offset;

		public override string ToString()
		{
			return string.Format("{0}:{1}", Line, Offset);
		}
	}
}
