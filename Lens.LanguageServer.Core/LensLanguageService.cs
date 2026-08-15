using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lens.Analysis;

namespace Lens.LanguageServer.Core
{
    /// <summary>
    /// Everything an editor wants to know about a LENS file, in terms no protocol owns.
    ///
    /// This is the reusable half of the language server. It holds the open documents, decides what
    /// to answer, and returns plain objects. A server speaking the language server protocol wraps
    /// it, and so could an editor plugin that runs in-process and never speaks a protocol at all.
    /// </summary>
    public sealed class LensLanguageService : IDisposable
    {
        #region Constructor

        public LensLanguageService(ScriptAnalyzer analyzer = null)
        {
            _analyzer = analyzer ?? new ScriptAnalyzer();
            _documents = new ConcurrentDictionary<string, LensDocument>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Fields

        private readonly ScriptAnalyzer _analyzer;
        private readonly ConcurrentDictionary<string, LensDocument> _documents;

        #endregion

        #region Documents

        /// <summary>
        /// Starts tracking a file, or replaces the copy already being tracked.
        /// </summary>
        public LensDocument Open(string uri, string text, int version = 0)
        {
            var document = new LensDocument(uri, text, version, _analyzer);

            _documents.AddOrUpdate(
                uri,
                document,
                (_, existing) =>
                {
                    existing.Dispose();
                    return document;
                }
            );

            return document;
        }

        /// <summary>
        /// Records a new version of a tracked file.
        /// </summary>
        public void Change(string uri, string text, int version)
        {
            if (_documents.TryGetValue(uri, out var document))
                document.Update(text, version);
            else
                Open(uri, text, version);
        }

        /// <summary>
        /// Stops tracking a file.
        /// </summary>
        public void Close(string uri)
        {
            if (_documents.TryRemove(uri, out var document))
                document.Dispose();
        }

        /// <summary>
        /// The tracked file, or null if it is not open.
        /// </summary>
        public LensDocument Find(string uri)
        {
            return _documents.TryGetValue(uri, out var document) ? document : null;
        }

        #endregion

        #region Features

        /// <summary>
        /// Everything wrong with a file: what the compiler reports, plus the references that do not
        /// resolve - which the compiler deliberately says nothing about, because the assemblies are
        /// the host's business and not the script's.
        /// </summary>
        public IReadOnlyList<Problem> Diagnose(string uri)
        {
            var document = Find(uri);
            if (document == null)
                return EmptyProblems;

            var result = document.Analysis.Diagnostics
                                 .Select(x => new Problem(x.Message, x.IsError ? ProblemSeverity.Error : ProblemSeverity.Warning, TextRange.FromSpan(x.Span)))
                                 .ToList();

            result.AddRange(MissingReferences(document));

            return result;
        }

        /// <summary>
        /// The names that may be written at a position.
        /// </summary>
        public IReadOnlyList<Suggestion> Suggest(string uri, TextPosition position)
        {
            var document = Find(uri);
            if (document == null)
                return EmptySuggestions;

            return document.Analysis
                           .Complete(position.ToLocation())
                           .Select(x => new Suggestion(x.Label, x.Kind, x.Detail))
                           .ToArray();
        }

        /// <summary>
        /// What sits at a position.
        /// </summary>
        public Explanation Explain(string uri, TextPosition position)
        {
            var document = Find(uri);
            if (document == null)
                return null;

            var text = document.Analysis.DescribeAt(position.ToLocation());

            return string.IsNullOrEmpty(text)
                ? null
                : new Explanation(text, document.WordAt(position));
        }

        /// <summary>
        /// Where the name at a position is declared.
        /// </summary>
        public DocumentLocation Define(string uri, TextPosition position)
        {
            var symbol = SymbolAt(uri, position);

            return symbol?.Declaration == null
                ? null
                : new DocumentLocation(uri, TextRange.FromSpan(symbol.Declaration.Value));
        }

        /// <summary>
        /// Everywhere the name at a position is written.
        /// </summary>
        public IReadOnlyList<DocumentLocation> FindReferences(string uri, TextPosition position)
        {
            var symbol = SymbolAt(uri, position);

            return symbol == null
                ? EmptyLocations
                : symbol.References.Select(x => new DocumentLocation(uri, TextRange.FromSpan(x))).ToArray();
        }

        /// <summary>
        /// The edits that rename the name at a position, or the reason there are none.
        /// </summary>
        public RenameOutcome Rename(string uri, TextPosition position, string newName)
        {
            var symbol = SymbolAt(uri, position);

            if (symbol == null)
                return RenameOutcome.Refused("There is nothing to rename here.");

            if (!symbol.CanRename)
                return RenameOutcome.Refused(symbol.RenameRefusal ?? "This name cannot be renamed.");

            if (!IsValidName(newName))
                return RenameOutcome.Refused($"'{newName}' is not a valid LENS name.");

            var document = Find(uri);
            if (document != null && ClashesWithExistingName(document, symbol, newName))
                return RenameOutcome.Refused($"'{newName}' is already used in this script.");

            var edits = symbol.References
                              .Select(x => new DocumentEdit(uri, TextRange.FromSpan(x), newName))
                              .ToArray();

            return RenameOutcome.Allowed(edits);
        }

        /// <summary>
        /// The declarations the file contains.
        /// </summary>
        public IReadOnlyList<OutlineEntry> Outline(string uri)
        {
            var document = Find(uri);

            return document == null
                ? EmptyOutline
                : document.Analysis.Outline.Select(Convert).ToArray();
        }

        /// <summary>
        /// The colouring of a file, split so that no run crosses a line break.
        /// </summary>
        public IReadOnlyList<ColouredRun> Colour(string uri)
        {
            var document = Find(uri);
            if (document == null)
                return EmptyRuns;

            var result = new List<ColouredRun>();

            foreach (var curr in document.Analysis.Tokens)
            {
                var range = TextRange.FromSpan(curr.Span);
                var type = SemanticTokenLegend.IndexOf(curr.Kind);

                if (range.Start.Line == range.End.Line)
                {
                    var length = range.End.Character - range.Start.Character;
                    if (length > 0)
                        result.Add(new ColouredRun(range.Start.Line, range.Start.Character, length, type));

                    continue;
                }

                // a verbatim string is one token and several lines; every protocol wants it as one
                // run per line
                for (var line = range.Start.Line; line <= range.End.Line; line++)
                {
                    var from = line == range.Start.Line ? range.Start.Character : 0;
                    var to = line == range.End.Line ? range.End.Character : document.LengthOfLine(line);

                    if (to > from)
                        result.Add(new ColouredRun(line, from, to - from, type));
                }
            }

            return result;
        }

        #endregion

        #region Helpers

        /// <summary>
        /// The name at a position, if there is one.
        /// </summary>
        private ScriptSymbol SymbolAt(string uri, TextPosition position)
        {
            return Find(uri)?.Analysis.FindSymbol(position.ToLocation());
        }

        /// <summary>
        /// The 'declare reference' lines that point at a file which is not there.
        ///
        /// A warning rather than an error, and reported here rather than by the compiler: the host
        /// decides which assemblies exist, so a path that does not resolve says nothing about
        /// whether the script will run - only that the editor cannot see what the script names.
        /// </summary>
        private IEnumerable<Problem> MissingReferences(LensDocument document)
        {
            var folder = FolderOf(document.Uri);

            foreach (var curr in document.Analysis.References)
            {
                if (string.IsNullOrWhiteSpace(curr.Path) || Exists(folder, curr.Path))
                    continue;

                yield return new Problem(
                    $"The referenced assembly '{curr.Path}' was not found. Names it provides cannot be checked.",
                    ProblemSeverity.Warning,
                    TextRange.FromSpan(curr.Span)
                );
            }
        }

        /// <summary>
        /// Whether a referenced assembly resolves, relative to the script when the path is relative.
        /// </summary>
        private static bool Exists(string folder, string path)
        {
            try
            {
                if (Path.IsPathRooted(path))
                    return File.Exists(path);

                return folder != null && File.Exists(Path.Combine(folder, path));
            }
            catch (Exception)
            {
                // an unusable path is a path that does not resolve
                return false;
            }
        }

        /// <summary>
        /// The folder a document lives in, as far as its uri says.
        /// </summary>
        private static string FolderOf(string uri)
        {
            try
            {
                if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && parsed.IsFile)
                    return Path.GetDirectoryName(parsed.LocalPath);

                return Path.GetDirectoryName(uri);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether a string is something LENS would accept as a name.
        /// </summary>
        private static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "_")
                return false;

            if (!char.IsLetter(name[0]) && name[0] != '_')
                return false;

            return name.All(x => char.IsLetterOrDigit(x) || x == '_') && !Reserved.Contains(name);
        }

        /// <summary>
        /// Whether renaming into a name would collide with one already in the file.
        ///
        /// Deliberately blunt: any other name in the script counts, not only the ones in scope. A
        /// rename that silently captures a name is the one failure mode of this feature that
        /// corrupts working code, and refusing a legal rename costs the user a different name.
        /// </summary>
        private static bool ClashesWithExistingName(LensDocument document, ScriptSymbol symbol, string newName)
        {
            if (newName == symbol.Name)
                return false;

            return document.Analysis.Tokens.Any(
                x => x.Text == newName
                     && (x.Kind == TokenKind.Variable
                         || x.Kind == TokenKind.Parameter
                         || x.Kind == TokenKind.Function
                         || x.Kind == TokenKind.Type
                         || x.Kind == TokenKind.Field)
            );
        }

        /// <summary>
        /// The editor's view of an outline entry.
        /// </summary>
        private static OutlineEntry Convert(OutlineItem item)
        {
            var selection = TextRange.FromSpan(item.Selection.IsEmpty ? item.Span : item.Selection);

            // an editor rejects an outline entry whose name is not inside the declaration it names,
            // and rejects the whole batch with it - so the declaration is widened to hold the name
            // rather than trusted to
            var range = TextRange.Union(TextRange.FromSpan(item.Span), selection);

            return new OutlineEntry(
                item.Name,
                item.Kind,
                item.Detail,
                range,
                selection,
                item.Children.Select(Convert).ToArray()
            );
        }

        private static readonly HashSet<string> Reserved = new HashSet<string>
        {
            "declare", "use", "record", "type", "fun", "pure", "let", "var", "new", "if", "then",
            "else", "while", "do", "for", "in", "try", "catch", "finally", "throw", "match", "with",
            "case", "when", "yield", "await", "using", "not", "is", "as", "of", "ref", "typeof",
            "default", "true", "false", "null"
        };

        private static readonly Problem[] EmptyProblems = new Problem[0];
        private static readonly Suggestion[] EmptySuggestions = new Suggestion[0];
        private static readonly DocumentLocation[] EmptyLocations = new DocumentLocation[0];
        private static readonly OutlineEntry[] EmptyOutline = new OutlineEntry[0];
        private static readonly ColouredRun[] EmptyRuns = new ColouredRun[0];

        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            foreach (var curr in _documents.Values)
                curr.Dispose();

            _documents.Clear();
        }

        #endregion
    }
}
