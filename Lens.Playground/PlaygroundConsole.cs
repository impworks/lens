using System;
using System.IO;
using System.Text;

namespace Lens.Playground
{
    /// <summary>
    /// The console a script writes to and reads from in the browser.
    ///
    /// Standard output is redirected once, at startup, and stays redirected: the writer is
    /// permanent and the runner takes whatever accumulated during one script. Standard input is
    /// redirected at the same moment for a different reason - reading Console.In on this platform
    /// throws unless something has been set first, so the redirection has to happen before any
    /// code touches the property, not when a script first asks for a line.
    /// </summary>
    internal static class PlaygroundConsole
    {
        #region Fields

        private static BufferWriter _writer;
        private static StringReader _reader;

        #endregion

        #region Methods

        /// <summary>
        /// Takes over the console for the lifetime of the page.
        ///
        /// Must be called before anything reads Console.In or Console.Out, which is why it happens
        /// during startup rather than on the first run.
        /// </summary>
        public static void Install()
        {
            if (_writer != null)
                return;

            _writer = new BufferWriter();
            Console.SetOut(_writer);
            Console.SetError(_writer);

            SetInput(string.Empty);
        }

        /// <summary>
        /// Points standard input at the text the user typed into the input pane.
        ///
        /// A script reads it line by line as it would read a terminal; when it runs out, ReadLine
        /// returns null, exactly as it does at the end of a redirected stream anywhere else.
        /// </summary>
        public static void SetInput(string text)
        {
            _reader = new StringReader(text ?? string.Empty);

            // The platform analyser marks SetIn as unsupported under a browser, and it is right
            // about the console this would otherwise reach: there is no terminal behind the tab,
            // and reading Console.In without setting it first throws. Setting it is the fix for
            // that rather than an instance of it - a redirected reader is just an object, and the
            // playground's whole input model rests on this working.
#pragma warning disable CA1416
            Console.SetIn(_reader);
#pragma warning restore CA1416
        }

        /// <summary>
        /// Everything written since the last time this was called, and clears the buffer.
        /// </summary>
        public static string TakeOutput()
        {
            return _writer.Take();
        }

        /// <summary>
        /// Whether anything has been written since the last take.
        /// </summary>
        public static bool HasOutput => _writer.HasContent;

        #endregion

        #region Nested classes

        /// <summary>
        /// A writer that keeps what it is given until somebody asks for it.
        ///
        /// Writing straight through to the page is not an option: the browser gives the script the
        /// one thread it has, so nothing the page is told during a run is painted until the script
        /// returns or awaits. Buffering here and flushing at those two moments shows the same text
        /// at the same times, without a JavaScript call per character.
        /// </summary>
        private sealed class BufferWriter : TextWriter
        {
            private readonly StringBuilder _buffer = new StringBuilder();

            public override Encoding Encoding => Encoding.UTF8;

            public bool HasContent
            {
                get { return _buffer.Length > 0; }
            }

            public override void Write(char value)
            {
                _buffer.Append(value);
            }

            public override void Write(string value)
            {
                _buffer.Append(value);
            }

            public string Take()
            {
                if (_buffer.Length == 0)
                    return string.Empty;

                var text = _buffer.ToString();
                _buffer.Clear();
                return text;
            }
        }

        #endregion
    }
}
