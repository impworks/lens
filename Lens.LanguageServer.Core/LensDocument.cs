using System;
using System.Collections.Generic;
using System.IO;
using Lens.Analysis;

namespace Lens.LanguageServer.Core
{
    /// <summary>
    /// One open file, and the reading of it.
    ///
    /// The analysis is produced on demand and kept until the text changes, because every feature
    /// asks for the same one: a hover, a completion and a diagnostic run all want the reading of
    /// the version the user is looking at.
    /// </summary>
    public sealed class LensDocument : IDisposable
    {
        #region Constructor

        internal LensDocument(string uri, string text, int version, ScriptAnalyzer analyzer)
        {
            Uri = uri;
            Version = version;

            _analyzer = analyzer;
            _text = text ?? string.Empty;
            _folder = FolderOf(uri);
        }

        #endregion

        #region Fields

        private readonly ScriptAnalyzer _analyzer;
        private readonly string _folder;

        private string _text;
        private ScriptAnalysis _analysis;
        private int[] _lineStarts;

        #endregion

        #region Properties

        /// <summary>
        /// How the editor names this file.
        /// </summary>
        public string Uri { get; }

        /// <summary>
        /// The editor's version counter, so that stale results can be dropped.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// The text as the editor last reported it.
        /// </summary>
        public string Text => _text;

        /// <summary>
        /// The reading of the current text.
        /// </summary>
        public ScriptAnalysis Analysis => _analysis ?? (_analysis = _analyzer.Analyze(_text, _folder));

        #endregion

        #region Methods

        /// <summary>
        /// Replaces the contents, discarding the reading of what was there before.
        /// </summary>
        public void Update(string text, int version)
        {
            _text = text ?? string.Empty;
            Version = version;

            _analysis?.Dispose();
            _analysis = null;
            _lineStarts = null;
        }

        /// <summary>
        /// The length of a line, which the colouring needs in order to split a token that spans
        /// several of them.
        /// </summary>
        public int LengthOfLine(int line)
        {
            var starts = LineStarts;

            if (line < 0 || line >= starts.Length)
                return 0;

            var end = line + 1 < starts.Length ? starts[line + 1] : _text.Length;

            // do not count the line break itself
            while (end > starts[line] && (_text[end - 1] == '\n' || _text[end - 1] == '\r'))
                end--;

            return end - starts[line];
        }

        /// <summary>
        /// The word the caret is inside or immediately after, which is what a rename is asking
        /// about and what a completion is filtering by.
        /// </summary>
        public TextRange WordAt(TextPosition position)
        {
            var starts = LineStarts;

            if (position.Line < 0 || position.Line >= starts.Length)
                return new TextRange(position, position);

            var lineStart = starts[position.Line];
            var lineLength = LengthOfLine(position.Line);
            var offset = Math.Min(position.Character, lineLength);

            var from = offset;
            while (from > 0 && IsWordChar(_text[lineStart + from - 1]))
                from--;

            var to = offset;
            while (to < lineLength && IsWordChar(_text[lineStart + to]))
                to++;

            return new TextRange(
                new TextPosition(position.Line, from),
                new TextPosition(position.Line, to)
            );
        }

        #endregion

        #region Helpers

        private static bool IsWordChar(char ch)
        {
            return char.IsLetterOrDigit(ch) || ch == '_';
        }

        /// <summary>
        /// The folder the file lives in, as far as its uri says - which is what a 'declare
        /// reference' entry with a relative path is resolved against. An unsaved buffer has none,
        /// and a reference by name works there just as well.
        /// </summary>
        private static string FolderOf(string uri)
        {
            try
            {
                if (System.Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile)
                    return Path.GetDirectoryName(parsed.LocalPath);

                return Path.GetDirectoryName(uri);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private int[] LineStarts
        {
            get
            {
                if (_lineStarts != null)
                    return _lineStarts;

                var starts = new List<int> {0};

                for (var i = 0; i < _text.Length; i++)
                {
                    if (_text[i] == '\n')
                        starts.Add(i + 1);
                }

                return _lineStarts = starts.ToArray();
            }
        }

        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            _analysis?.Dispose();
            _analysis = null;
        }

        #endregion
    }
}
