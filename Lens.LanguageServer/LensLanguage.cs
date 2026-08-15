using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Lens.LanguageServer
{
    /// <summary>
    /// How the editor identifies a LENS file.
    /// </summary>
    internal static class LensLanguage
    {
        /// <summary>
        /// The language id, which has to match the one the editor extension declares.
        /// </summary>
        public const string Id = "lens";

        /// <summary>
        /// The files this server answers for.
        /// </summary>
        public static readonly TextDocumentSelector Selector = TextDocumentSelector.ForLanguage(Id);
    }
}
