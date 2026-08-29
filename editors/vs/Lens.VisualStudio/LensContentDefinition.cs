using System.ComponentModel.Composition;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Utilities;

namespace Lens.VisualStudio
{
    /// <summary>
    /// Tells Visual Studio that a .lns file is a LENS file.
    ///
    /// A content type is the only trigger an LSP client has: nothing in this extension is loaded
    /// until a file of this type is opened, and the language client below is matched to the server
    /// by the same name.
    /// </summary>
    public static class LensContentDefinition
    {
        /// <summary>
        /// The name shared by the content type, the language client and the TextMate language
        /// configuration mapping in LensGrammars.pkgdef.
        /// </summary>
        public const string ContentTypeName = "lens";

        /// <summary>
        /// Deriving from the code-remote content type is what makes the editor route requests for
        /// this file through the language server client rather than expecting a local language
        /// service.
        /// </summary>
        [Export]
        [Name(ContentTypeName)]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
        internal static ContentTypeDefinition LensContentTypeDefinition = null;

        [Export]
        [FileExtension(".lns")]
        [ContentType(ContentTypeName)]
        internal static FileExtensionToContentTypeDefinition LensFileExtensionDefinition = null;
    }
}
