using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Lens.Analysis;

namespace Lens.Playground
{
    /// <summary>
    /// Compiles and runs one script, and turns whatever happened into something the page can show.
    ///
    /// Everything happens on the browser's only thread, which has two consequences the rest of
    /// this class is shaped by. A script is awaited rather than waited for, because a script may
    /// await at its top level and blocking on it here would deadlock. And a script that never
    /// yields holds the thread until it finishes, so output is flushed at every moment the thread
    /// is free rather than every time a line is written.
    /// </summary>
    internal sealed class ScriptRunner
    {
        #region Constructor

        public ScriptRunner(Action<string> flushOutput)
        {
            _flushOutput = flushOutput;
            _analyzer = PlaygroundOptions.CreateAnalyzer();
        }

        #endregion

        #region Fields

        private readonly Action<string> _flushOutput;
        private readonly ScriptAnalyzer _analyzer;

        #endregion

        #region Methods

        /// <summary>
        /// Compiles the source and runs it, with the input pane's text as standard input.
        /// </summary>
        public async Task<RunResultDto> RunAsync(string source, string input)
        {
            PlaygroundConsole.SetInput(input);

            var refused = RefuseDeclaredReferences(source);
            if (refused != null)
                return refused;

            var timer = Stopwatch.StartNew();
            var pump = PumpOutputAsync();

            try
            {
                using (var compiler = PlaygroundOptions.CreateCompiler())
                {
                    // CompileAsync rather than Compile: the page has a synchronization context, so
                    // a script that awaits at its top level can only be awaited, never waited for
                    var script = compiler.CompileAsync(source);
                    var value = await script();

                    return Finish(timer, new RunResultDto
                    {
                        Result = ValueFormatter.Format(value),
                        ResultType = ValueFormatter.TypeNameOf(value)
                    });
                }
            }
            catch (LensCompilerException ex)
            {
                return Finish(timer, new RunResultDto
                {
                    Error = PlatformLimits.Explain(ex),
                    ErrorRange = RangeOf(ex),
                    IsCompileError = true
                });
            }
            catch (Exception ex)
            {
                return Finish(timer, new RunResultDto
                {
                    Error = ex.GetType().Name + ": " + PlatformLimits.Explain(ex)
                });
            }
            finally
            {
                _pumping = false;
                await pump;
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Turns a 'declare reference' line into a refusal the user can act on.
        ///
        /// The playground has no libraries to load and no disk to load them from, so the entry
        /// could only ever fail; saying so in one sentence beats letting the resolver report that
        /// it looked for a file and found none.
        /// </summary>
        private RunResultDto RefuseDeclaredReferences(string source)
        {
            using (var analysis = _analyzer.Analyze(source))
            {
                var reference = analysis.References.FirstOrDefault();
                if (reference == null)
                    return null;

                return new RunResultDto
                {
                    Error = string.Format(
                        "'declare reference' is not available in the playground: there is no file system to load '{0}' from. "
                        + "The .NET base class libraries, including LINQ and HttpClient, are already referenced.",
                        reference.Path
                    ),
                    ErrorRange = Interop.ToRange(Lens.LanguageServer.Core.TextRange.FromSpan(reference.Span)),
                    IsCompileError = true
                };
            }
        }

        /// <summary>
        /// Sends whatever the script has printed to the page, repeatedly, for as long as it runs.
        ///
        /// The delay is what makes this work: its continuation is queued on the browser's thread,
        /// so it runs at exactly the moments the script is not running - each time it awaits, and
        /// once it has returned. A script that never awaits produces one flush, at the end, which
        /// is the most a single-threaded page can offer.
        /// </summary>
        private async Task PumpOutputAsync()
        {
            _pumping = true;

            while (_pumping)
            {
                await Task.Delay(50);
                Drain();
            }
        }

        private bool _pumping;

        /// <summary>
        /// Hands the page everything printed since the last time.
        /// </summary>
        private void Drain()
        {
            if (PlaygroundConsole.HasOutput)
                _flushOutput(PlaygroundConsole.TakeOutput());
        }

        /// <summary>
        /// Fills in the parts of the result that every outcome has.
        /// </summary>
        private RunResultDto Finish(Stopwatch timer, RunResultDto result)
        {
            timer.Stop();

            result.ElapsedMs = timer.Elapsed.TotalMilliseconds;
            result.Output = PlaygroundConsole.TakeOutput();

            return result;
        }

        /// <summary>
        /// Where a compiler error points, when it points anywhere.
        /// </summary>
        private static RangeDto RangeOf(LensCompilerException ex)
        {
            if (ex.StartLocation == null)
                return null;

            var start = Lens.LanguageServer.Core.TextPosition.FromLocation(ex.StartLocation.Value);
            var end = ex.EndLocation != null
                ? Lens.LanguageServer.Core.TextPosition.FromLocation(ex.EndLocation.Value)
                : start;

            return Interop.ToRange(new Lens.LanguageServer.Core.TextRange(start, end));
        }

        #endregion
    }
}
