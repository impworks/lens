using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Lens.Compiler.Entities;
using Lens.Translations;

#if NET_MODERN
using System.IO;
using System.Runtime.Loader;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;
#endif

namespace Lens.Compiler
{
    internal partial class Context
    {
        #region Fields

        private AssemblyBuilder _mainAssembly;
        private ModuleBuilder _mainModule;

        /// <summary>
        /// The text the script was read from, kept only for as long as it takes to put a copy of it
        /// inside the symbols. A script compiled from a tree that no source produced has none.
        /// </summary>
        private string _sourceText;

#if NET_MODERN
        /// <summary>
        /// The emit target when symbols are being written, which is a different kind of builder
        /// rather than the same one configured differently.
        ///
        /// Nothing a persisted builder defines can be executed: it describes an assembly instead of
        /// being one. That is precisely what makes it able to write symbols, and it is why a
        /// debuggable script is loaded back from the image it produces.
        /// </summary>
        private PersistedAssemblyBuilder _persistedAssembly;
#endif

        #endregion

        #region Properties

        /// <summary>
        /// Records where the emitted IL came from, or null when this compilation writes no symbols.
        /// </summary>
        internal DebugInfoWriter DebugInfo { get; private set; }

        /// <summary>
        /// The symbols this compilation produced, kept so that they can be read back.
        ///
        /// A debugger is the consumer of what is written here, and no test can attach one. Keeping
        /// the bytes lets the sequence points be read with a metadata reader and checked against the
        /// lines of the script they claim to describe - which is the only way to know that stepping
        /// through a loop will land where its author expects.
        /// </summary>
        internal byte[] DebugSymbols { get; private set; }

        #endregion

        #region Emit target

        /// <summary>
        /// Remembers the text the script was read from, so that a debuggable compilation can carry
        /// it inside its symbols.
        /// </summary>
        internal void SetSource(string src)
        {
            _sourceText = src;
        }

        /// <summary>
        /// Creates the assembly and module to emit into, unless that has already happened.
        ///
        /// Everything before this point is analysis, and used to be impossible to separate: the
        /// constructor built an assembly whether or not anything was ever going to be emitted.
        /// </summary>
        private void EnsureEmitTarget()
        {
            if (_mainAssembly != null)
                return;

            AssemblyName an;
            lock (typeof(Context))
                an = new AssemblyName(Unique.AssemblyName());

#if NET_CLASSIC
            if (Options.AllowSave)
            {
                if (string.IsNullOrEmpty(Options.FileName))
                    Options.FileName = an.Name + (Options.SaveAsExe ? ".exe" : ".dll");

                _mainAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndSave, DebugAttributes());
                _mainModule = _mainAssembly.DefineDynamicModule(an.Name, Options.FileName, IsDebug);
            }
            else
            {
                _mainAssembly = AppDomain.CurrentDomain.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndCollect, DebugAttributes());
                _mainModule = _mainAssembly.DefineDynamicModule(an.Name, IsDebug);
            }
#elif NET_MODERN
            if (IsDebug)
            {
                _persistedAssembly = new PersistedAssemblyBuilder(an, typeof(object).Assembly, DebugAttributes());
                _mainAssembly = _persistedAssembly;
            }
            else
            {
                _mainAssembly = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndCollect);
            }

            _mainModule = _mainAssembly.DefineDynamicModule(an.Name);
#else
            if (IsDebug)
                throw new NotSupportedException(CompilerMessages.DebugNotSupported);

            _mainAssembly = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndCollect);
            _mainModule = _mainAssembly.DefineDynamicModule(an.Name);
#endif

            if (IsDebug)
                CreateDebugInfo(an);
        }

        /// <summary>
        /// Tells the runtime that the code in this assembly is meant to be looked at rather than to
        /// run as fast as possible.
        ///
        /// Without this the JIT is free to reuse a slot whose value is no longer read, and a
        /// debugger stopped a line later would report the variable as unavailable - which reads to
        /// whoever is debugging as the compiler having lost it.
        /// </summary>
        private IEnumerable<CustomAttributeBuilder> DebugAttributes()
        {
            if (!IsDebug)
                return null;

            var modes = DebuggableAttribute.DebuggingModes.Default
                        | DebuggableAttribute.DebuggingModes.DisableOptimizations;

            return new[]
            {
                new CustomAttributeBuilder(
                    typeof(DebuggableAttribute).GetConstructor(new[] {typeof(DebuggableAttribute.DebuggingModes)}),
                    new object[] {modes}
                )
            };
        }

        /// <summary>
        /// Declares the source file that the symbols of this compilation refer to.
        /// </summary>
        private void CreateDebugInfo(AssemblyName an)
        {
            var path = Options.DebugSettings.SourceFile;
            if (string.IsNullOrEmpty(path))
                path = an.Name + ".lns";

            var language = Options.DebugSettings.ReportAsCSharp
                ? DebugInfoWriter.CSharpLanguageGuid
                : DebugInfoWriter.LanguageGuid;

#if NET_CLASSIC
            DebugInfo = new DebugInfoWriter(_mainModule.DefineDocument(path, language, Guid.Empty, Guid.Empty), _sourceText);
#elif NET_MODERN
            DebugInfo = new DebugInfoWriter(_mainModule.DefineDocument(path, language), _sourceText);
#endif
        }

        #endregion

        #region Script materialization

        /// <summary>
        /// Creates the object the host is handed: an instance of the script's entry point class.
        ///
        /// A script compiled to run is instantiated where it was built. A script compiled to be
        /// debugged was described rather than built, so the description is turned into an image,
        /// the image is loaded with its symbols beside it, and the instance comes from there.
        /// </summary>
        private object CreateScriptInstance()
        {
#if NET_MODERN
            if (_persistedAssembly != null)
            {
                var assembly = LoadDebuggableAssembly();
                return Activator.CreateInstance(assembly.GetType(EntityNames.MainTypeName, throwOnError: true));
            }
#endif

            return Activator.CreateInstance(ResolveType(EntityNames.MainTypeName).Materialize());
        }

#if NET_MODERN

        /// <summary>
        /// Serializes the described assembly along with its symbols and loads both.
        ///
        /// The symbols are handed to the loader rather than left on disk: the assembly has no place
        /// on disk to be beside, and this is what lets a debugger - and a stack trace - map the
        /// script's IL back to the lines it was written as.
        /// </summary>
        private Assembly LoadDebuggableAssembly()
        {
            var metadata = _persistedAssembly.GenerateMetadata(out var ilStream, out var fieldData, out var pdbBuilder);

            EmbedSource(pdbBuilder);

            var symbolsBlob = new BlobBuilder();
            var symbols = new PortablePdbBuilder(pdbBuilder, metadata.GetRowCounts(), default);
            var symbolsId = symbols.Serialize(symbolsBlob);

            var debugDirectory = new DebugDirectoryBuilder();
            debugDirectory.AddCodeViewEntry(_mainAssembly.GetName().Name + ".pdb", symbolsId, symbols.FormatVersion);

            var image = new ManagedPEBuilder(
                new PEHeaderBuilder(imageCharacteristics: Characteristics.ExecutableImage | Characteristics.Dll),
                new MetadataRootBuilder(metadata),
                ilStream,
                mappedFieldData: fieldData,
                debugDirectoryBuilder: debugDirectory
            );

            var imageBlob = new BlobBuilder();
            image.Serialize(imageBlob);

            DebugSymbols = symbolsBlob.ToArray();

            // loaded into a collectible context, so that a debuggable script costs the host no more
            // permanently than an ordinary one, which is emitted RunAndCollect. Nothing is resolved
            // there: a context with no Load of its own falls back to the one the host lives in, which
            // is where LENS itself and everything the host registered already are.
            var context = new AssemblyLoadContext(_mainAssembly.GetName().Name, isCollectible: true);

            return context.LoadFromStream(
                new MemoryStream(imageBlob.ToArray()),
                new MemoryStream(DebugSymbols)
            );
        }

        /// <summary>
        /// Stores the script's own text inside its symbols.
        ///
        /// A script is usually not a file. It is a string the host built, or read out of a database,
        /// or received over a wire - and a debugger that is given only a path has nothing to open.
        /// Carrying the source along is what makes such a script steppable at all.
        /// </summary>
        private void EmbedSource(MetadataBuilder pdbBuilder)
        {
            if (!Options.DebugSettings.EmbedSource || string.IsNullOrEmpty(_sourceText))
                return;

            // the document was declared before anything was emitted and is the only one, so it is
            // the first row of the table - but the compilation is not worth failing over a guess
            if (pdbBuilder.GetRowCounts()[(int) TableIndex.Document] != 1)
                return;

            var content = new BlobBuilder();

            // the format prefix of an embedded source: zero for text stored as it is, and the
            // decompressed size when it is deflated. A script is small enough not to be worth it.
            content.WriteInt32(0);
            content.WriteBytes(new UTF8Encoding(false).GetBytes(_sourceText));

            pdbBuilder.AddCustomDebugInformation(
                MetadataTokens.DocumentHandle(1),
                pdbBuilder.GetOrAddGuid(EmbeddedSourceKind),
                pdbBuilder.GetOrAddBlob(content)
            );
        }

        /// <summary>
        /// The identifier under which a portable symbol file carries the text of a source document.
        /// Defined by the portable PDB format, and understood by every .NET debugger.
        /// </summary>
        private static readonly Guid EmbeddedSourceKind = new Guid("0e8a571b-6926-466e-b4ad-8ab04611f5fe");

#endif

        #endregion
    }
}
