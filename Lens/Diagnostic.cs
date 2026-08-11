using Lens.SyntaxTree;
using Lens.Translations;

namespace Lens
{
    /// <summary>
    /// The severity of a compiler diagnostic.
    /// </summary>
    public enum DiagnosticSeverity
    {
        Warning,
        Error
    }

    /// <summary>
    /// A single problem discovered while compiling a script.
    /// Unlike an exception, a diagnostic does not stop the compilation: the compiler collects as
    /// many of them as it can before giving up.
    /// </summary>
    public class Diagnostic
    {
        #region Constructors

        internal Diagnostic(DiagnosticSeverity severity, string message, LexemLocation? start, LexemLocation? end)
        {
            Severity = severity;
            Message = message;
            StartLocation = start;
            EndLocation = end;
        }

        internal Diagnostic(LensCompilerException ex)
            : this(DiagnosticSeverity.Error, ex.Message, ex.StartLocation, ex.EndLocation)
        {
            Exception = ex;
        }

        #endregion

        #region Properties

        /// <summary>
        /// How serious the problem is.
        /// </summary>
        public DiagnosticSeverity Severity { get; }

        /// <summary>
        /// The human-readable description of the problem.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Start of the erroneous segment, if known.
        /// </summary>
        public LexemLocation? StartLocation { get; }

        /// <summary>
        /// End of the erroneous segment, if known.
        /// </summary>
        public LexemLocation? EndLocation { get; }

        /// <summary>
        /// The message with the location appended, if there is one.
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

        #endregion

        #region Fields

        /// <summary>
        /// The exception this diagnostic was created from, if any.
        /// It is rethrown as-is by the public API, so that locations and inner exceptions survive.
        /// </summary>
        internal readonly LensCompilerException Exception;

        #endregion

        #region Methods

        /// <summary>
        /// Returns the exception that represents this diagnostic.
        /// </summary>
        internal LensCompilerException AsException()
        {
            if (Exception != null)
                return Exception;

            var ex = new LensCompilerException(Message);
            if (StartLocation != null)
                ex.BindToLocation(StartLocation.Value, EndLocation ?? StartLocation.Value);

            return ex;
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return $"{Severity}: {FullMessage}";
        }

        #endregion
    }
}
