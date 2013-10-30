using System;
using Lens.SyntaxTree;
using Lens.Translations;

namespace Lens
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
			BindToLocation(entity);
		}

		/// <summary>
		/// Start of the erroneous segment.
		/// </summary>
		public LexemLocation? StartLocation { get; private set; }

		/// <summary>
		/// End of the erroneous segment.
		/// </summary>
		public LexemLocation? EndLocation { get; private set; }

		/// <summary>
		/// Full message with error positions.
		/// </summary>
		public string FullMessage
		{
			get
			{
				if (StartLocation == null && EndLocation == null)
					return Message;

				return EndLocation == null
					? string.Format(Message + "\n" + CompilerMessages.Location, StartLocation.Value)
					: string.Format(Message + "\n" + CompilerMessages.LocationSpan, StartLocation.Value, EndLocation.Value);
			}
		}

		/// <summary>
		/// Bind exception to a location.
		/// </summary>
		public LensCompilerException BindToLocation(LocationEntity entity)
		{
			return entity == null ? this : BindToLocation(entity.StartLocation, entity.EndLocation);
		}

		/// <summary>
		/// Bind exception to a location.
		/// </summary>
		public LensCompilerException BindToLocation(LexemLocation start, LexemLocation end)
		{
			if(start.Line != 0 || start.Offset != 0)
				StartLocation = start;

			if(end.Line != 0 || end.Offset != 0)
				EndLocation = end;

			return this;
		}
	}
}
