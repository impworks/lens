using System;
using Lens.SyntaxTree.Utils;

namespace Lens.SyntaxTree
{
	/// <summary>
	/// A generic exception that has occured during parse.
	/// </summary>
	public class LensCompilerException : Exception
	{
		public LensCompilerException(string msg) : base(msg)
		{ }

		public LensCompilerException(string msg, LocationEntity entity) : base(msg)
		{
			StartLocation = entity.StartLocation;
			EndLocation = entity.EndLocation;
		}

		public LensCompilerException(string msg, LexemLocation start, LexemLocation end) : base(msg)
		{
			StartLocation = start;
			EndLocation = end;
		}

		/// <summary>
		/// Start of the erroneous segment.
		/// </summary>
		public LexemLocation StartLocation { get; private set; }

		/// <summary>
		/// End of the erroneous segment.
		/// </summary>
		public LexemLocation EndLocation { get; private set; }

		/// <summary>
		/// Full message with error positions.
		/// </summary>
		public string FullMessage
		{
			get
			{
				return string.Format(
					"{0}\nLocation: {1}:{2} ... {3}:{4}",
					Message,
					StartLocation.Line,
					StartLocation.Offset,
					EndLocation.Line,
					EndLocation.Offset
				);
			}
		}
	}
}
