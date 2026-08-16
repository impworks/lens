using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lens.Compiler;
using Lens.Lexer;
using Lens.Parser;
using Lens.SyntaxTree;

namespace Lens.Analysis
{
    /// <summary>
    /// Reads a script the way an editor needs it read: tolerantly, repeatedly, and without emitting
    /// anything.
    ///
    /// The analyzer is configuration - assemblies, safe mode, the options - and is reused across
    /// every keystroke. Each <see cref="Analyze"/> produces a fresh <see cref="ScriptAnalysis"/>,
    /// because binding memoizes and a context can only answer for one version of one file.
    ///
    /// Declarations are the source of truth here: an editor has no host, so a 'declare' block
    /// creates the environment rather than being checked against one. See
    /// <see cref="DeclarationMode"/>.
    /// </summary>
    public sealed class ScriptAnalyzer
    {
        #region Constructor

        public ScriptAnalyzer(LensCompilerOptions options = null)
        {
            _options = (options ?? new LensCompilerOptions()).Copy();
            _options.DeclarationMode = DeclarationMode.Provide;
            _options.MeasureTime = false;

            _assemblies = new List<Assembly>();
        }

        #endregion

        #region Fields

        private readonly LensCompilerOptions _options;
        private readonly List<Assembly> _assemblies;

        #endregion

        #region Methods

        /// <summary>
        /// Adds an assembly whose types the analysed scripts may name.
        ///
        /// An in-process host that already knows what it registers should call this; a standalone
        /// language server has only what the script declares plus the default assemblies.
        /// </summary>
        public void AddReference(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            _assemblies.Add(assembly);
        }

        /// <summary>
        /// Lexes, parses and binds a script, collecting everything that went wrong instead of
        /// stopping at the first problem.
        /// </summary>
        /// <param name="source">The text of the script.</param>
        /// <param name="baseDirectory">
        /// The folder the script lives in, if it has one: a 'declare reference' entry may name a
        /// library by a path relative to it.
        /// </param>
        public ScriptAnalysis Analyze(string source, string baseDirectory = null)
        {
            source = source ?? string.Empty;

            Exception fatal = null;

            // every stage is fenced off separately, so that a failure in one keeps whatever the
            // stages before it produced: a file that does not parse still has tokens to colour.
            // An editor that crashes on a half-typed line is worse than one that says less.
            var lexer = Guard(() => new LensLexer(source, true), () => new LensLexer(string.Empty, true), ref fatal);
            var parser = Guard(() => new LensParser(lexer.Lexems, true), () => new LensParser(EmptyLexems, true), ref fatal);

            var context = new Context(OptionsFor(baseDirectory)) {TrackTypeReferences = true};

            foreach (var curr in _assemblies)
                context.RegisterAssembly(curr);

            Guard<object>(
                () =>
                {
                    context.Analyze(parser.Nodes);
                    return null;
                },
                () => null,
                ref fatal
            );

            return new ScriptAnalysis(this, source, baseDirectory, lexer, parser, context, fatal);
        }

        /// <summary>
        /// Analyses a variant of the source, for the questions that need one - completion after a
        /// dot, where the source as written does not parse.
        /// </summary>
        internal ScriptAnalysis AnalyzeVariant(string source, string baseDirectory)
        {
            return Analyze(source, baseDirectory);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// The options for one analysis, which differ from the configured ones only in where a
        /// relative reference is looked for - and that is a property of the file, not of the
        /// analyzer, which serves every file the editor has open.
        /// </summary>
        private LensCompilerOptions OptionsFor(string baseDirectory)
        {
            if (string.IsNullOrEmpty(baseDirectory) || baseDirectory == _options.ScriptDirectory)
                return _options;

            var result = _options.Copy();
            result.ScriptDirectory = baseDirectory;
            return result;
        }

        /// <summary>
        /// Runs one stage of the reading, falling back to an empty result and recording the first
        /// failure rather than letting it reach the caller.
        /// </summary>
        private static T Guard<T>(Func<T> stage, Func<T> fallback, ref Exception fatal)
        {
            try
            {
                return stage();
            }
            catch (Exception ex)
            {
                fatal = fatal ?? ex;
                return fallback();
            }
        }

        private static readonly Lexem[] EmptyLexems = {new Lexem(LexemType.Eof, default(LexemLocation), default(LexemLocation))};

        #endregion
    }
}
