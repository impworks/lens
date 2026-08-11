using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lens.SyntaxTree;

namespace Lens.Compiler
{
    /// <summary>
    /// The collection of diagnostics gathered during a single compilation.
    /// </summary>
    internal class DiagnosticBag : IEnumerable<Diagnostic>
    {
        #region Fields

        private readonly List<Diagnostic> _items = new List<Diagnostic>();

        #endregion

        #region Properties

        /// <summary>
        /// Checks whether anything at all has been reported.
        /// </summary>
        public bool IsEmpty => _items.Count == 0;

        /// <summary>
        /// The number of diagnostics recorded so far.
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// Checks whether at least one error (as opposed to a warning) has been reported.
        /// </summary>
        public bool HasErrors => _items.Any(x => x.Severity == DiagnosticSeverity.Error);

        #endregion

        #region Methods

        /// <summary>
        /// Records a diagnostic created from an exception that has been caught at a recovery point.
        /// </summary>
        public void Add(LensCompilerException ex)
        {
            Add(new Diagnostic(ex));
        }

        /// <summary>
        /// Records a diagnostic bound to a location in the source code.
        /// </summary>
        public void Add(DiagnosticSeverity severity, LocationEntity entity, string message)
        {
            LexemLocation? start = null;
            LexemLocation? end = null;

            if (entity != null)
            {
                if (entity.StartLocation.Line != 0 || entity.StartLocation.Offset != 0)
                    start = entity.StartLocation;

                if (entity.EndLocation.Line != 0 || entity.EndLocation.Offset != 0)
                    end = entity.EndLocation;
            }

            Add(new Diagnostic(severity, message, start, end));
        }

        /// <summary>
        /// Records a diagnostic, unless an identical one has already been recorded.
        /// A single mistake can be rediscovered by several passes, and reporting it twice is noise.
        /// </summary>
        public void Add(Diagnostic diag)
        {
            var isDuplicate = _items.Any(x =>
                x.Severity == diag.Severity
                && x.Message == diag.Message
                && Equals(x.StartLocation?.Line, diag.StartLocation?.Line)
                && Equals(x.StartLocation?.Offset, diag.StartLocation?.Offset)
            );

            if (!isDuplicate)
                _items.Add(diag);
        }

        /// <summary>
        /// Returns the first error, or null if there are none.
        /// This is the diagnostic the public API surfaces as an exception.
        /// </summary>
        public Diagnostic FirstError()
        {
            return _items.FirstOrDefault(x => x.Severity == DiagnosticSeverity.Error);
        }

        #endregion

        #region IEnumerable<Diagnostic> implementation

        public IEnumerator<Diagnostic> GetEnumerator()
        {
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}
