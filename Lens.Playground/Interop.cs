using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lens.LanguageServer.Core;
using Microsoft.JSInterop;

namespace Lens.Playground
{
    /// <summary>
    /// Everything the page is allowed to call, and nothing else.
    ///
    /// The editor is JavaScript and the language services are .NET, so one of the two has to reach
    /// across. It is the page that reaches: it knows when a keystroke happened and what the user
    /// is pointing at, and this class answers. Nothing here holds page state.
    ///
    /// Positions crossing this boundary are zero-based, the way the language service counts them.
    /// The editor counts from one, and converts on its side, in one place.
    /// </summary>
    public static class Interop
    {
        #region Fields

        /// <summary>
        /// The single document the playground edits. It still needs a name, because the language
        /// service tracks documents by one.
        /// </summary>
        private const string DocumentUri = "playground://script.lns";

        private static LensLanguageService _service;
        private static ScriptRunner _runner;
        private static IJSInProcessRuntime _js;
        private static int _version;

        #endregion

        #region Startup

        /// <summary>
        /// Builds the services the page will call into.
        ///
        /// Called once from startup, before the page is shown, so that the first keystroke does
        /// not pay for it.
        /// </summary>
        internal static void Initialize(IJSInProcessRuntime js)
        {
            _js = js;
            _service = new LensLanguageService(PlaygroundOptions.CreateAnalyzer());
            _runner = new ScriptRunner(FlushOutput);

            _service.Open(DocumentUri, string.Empty);
        }

        /// <summary>
        /// Compiles and throws away a trivial script, so that the assembly scanning and the type
        /// caches every later compilation reuses are already warm.
        ///
        /// Without this the user pays a quarter of a second on their first run, and reads it as
        /// the language being slow rather than as the cache being cold.
        /// </summary>
        internal static void WarmUp()
        {
            try
            {
                using (var compiler = PlaygroundOptions.CreateCompiler())
                    compiler.Run("1 + 1");

                _service.Change(DocumentUri, "1 + 1", ++_version);
                _service.Diagnose(DocumentUri);
                _service.Change(DocumentUri, string.Empty, ++_version);
            }
            catch
            {
                // a warm-up that fails costs a slower first run and nothing else
            }
        }

        #endregion

        #region Documents

        /// <summary>
        /// Records the text now in the editor. Every other call answers about this text.
        /// </summary>
        [JSInvokable]
        public static void Update(string text)
        {
            _service.Change(DocumentUri, text ?? string.Empty, ++_version);
        }

        #endregion

        #region Language services

        /// <summary>
        /// The problems in the current text.
        /// </summary>
        [JSInvokable]
        public static List<ProblemDto> Diagnose()
        {
            return _service.Diagnose(DocumentUri)
                           .Select(x => new ProblemDto
                           {
                               Message = x.Message,
                               Severity = x.Severity == ProblemSeverity.Error ? "error" : "warning",
                               Range = ToRange(x.Range)
                           })
                           .ToList();
        }

        /// <summary>
        /// The names worth offering at a position.
        /// </summary>
        [JSInvokable]
        public static List<SuggestionDto> Suggest(int line, int character)
        {
            return _service.Suggest(DocumentUri, new TextPosition(line, character))
                           .Select(x => new SuggestionDto
                           {
                               Label = x.Label,
                               Kind = x.Kind.ToString(),
                               Detail = x.Detail
                           })
                           .ToList();
        }

        /// <summary>
        /// What the thing at a position is.
        /// </summary>
        [JSInvokable]
        public static ExplanationDto Explain(int line, int character)
        {
            var result = _service.Explain(DocumentUri, new TextPosition(line, character));

            return result == null
                ? null
                : new ExplanationDto {Text = result.Text, Range = ToRange(result.Range)};
        }

        /// <summary>
        /// Where the thing at a position is declared.
        /// </summary>
        [JSInvokable]
        public static RangeDto Define(int line, int character)
        {
            var result = _service.Define(DocumentUri, new TextPosition(line, character));

            return result == null ? null : ToRange(result.Range);
        }

        /// <summary>
        /// Everywhere the thing at a position is used.
        /// </summary>
        [JSInvokable]
        public static List<RangeDto> FindReferences(int line, int character)
        {
            return _service.FindReferences(DocumentUri, new TextPosition(line, character))
                           .Select(x => ToRange(x.Range))
                           .ToList();
        }

        /// <summary>
        /// The edits that renaming the thing at a position would need, or why it cannot be renamed.
        /// </summary>
        [JSInvokable]
        public static RenameDto Rename(int line, int character, string newName)
        {
            var outcome = _service.Rename(DocumentUri, new TextPosition(line, character), newName);

            return new RenameDto
            {
                IsAllowed = outcome.IsAllowed,
                Refusal = outcome.Refusal,
                Edits = outcome.Edits.Select(x => new EditDto {Range = ToRange(x.Range), Text = x.Text}).ToList()
            };
        }

        /// <summary>
        /// The declarations in the current text, for the outline.
        /// </summary>
        [JSInvokable]
        public static List<OutlineDto> Outline()
        {
            return _service.Outline(DocumentUri).Select(ToOutline).ToList();
        }

        /// <summary>
        /// The semantic colouring of the current text, flattened the way the editor wants it:
        /// five numbers per token, each position relative to the token before it.
        /// </summary>
        [JSInvokable]
        public static int[] Colour()
        {
            var runs = _service.Colour(DocumentUri);
            var data = new List<int>(runs.Count * 5);

            var lastLine = 0;
            var lastStart = 0;

            foreach (var run in runs)
            {
                var deltaLine = run.Line - lastLine;
                var deltaStart = deltaLine == 0 ? run.Character - lastStart : run.Character;

                data.Add(deltaLine);
                data.Add(deltaStart);
                data.Add(run.Length);
                data.Add(run.TokenType);
                data.Add(0);

                lastLine = run.Line;
                lastStart = run.Character;
            }

            return data.ToArray();
        }

        /// <summary>
        /// The colour names the indices above refer to, so that the page can register the legend
        /// without repeating the list.
        /// </summary>
        [JSInvokable]
        public static string[] TokenLegend()
        {
            return SemanticTokenLegend.TokenTypes;
        }

        #endregion

        #region Running

        /// <summary>
        /// Compiles and runs the given source, with the input pane's text as standard input.
        /// </summary>
        [JSInvokable]
        public static Task<RunResultDto> Run(string source, string input)
        {
            return _runner.RunAsync(source ?? string.Empty, input ?? string.Empty);
        }

        /// <summary>
        /// The scripts offered in the samples menu.
        /// </summary>
        [JSInvokable]
        public static List<SampleDto> Samples()
        {
            return SampleLibrary.All();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Pushes console output to the page while a script is still running.
        ///
        /// In-process rather than awaited on purpose: this is called from inside a run, where
        /// waiting on the browser would mean waiting on the thread the script is holding.
        /// </summary>
        private static void FlushOutput(string text)
        {
            _js.InvokeVoid("lensPlayground.appendOutput", text);
        }

        /// <summary>
        /// The editor's view of a language service range.
        /// </summary>
        internal static RangeDto ToRange(TextRange range)
        {
            return new RangeDto
            {
                StartLine = range.Start.Line,
                StartColumn = range.Start.Character,
                EndLine = range.End.Line,
                EndColumn = range.End.Character
            };
        }

        private static OutlineDto ToOutline(OutlineEntry entry)
        {
            return new OutlineDto
            {
                Name = entry.Name,
                Kind = entry.Kind.ToString(),
                Detail = entry.Detail,
                Range = ToRange(entry.Range),
                Selection = ToRange(entry.Selection),
                Children = entry.Children == null
                    ? new List<OutlineDto>()
                    : entry.Children.Select(ToOutline).ToList()
            };
        }

        #endregion
    }
}
