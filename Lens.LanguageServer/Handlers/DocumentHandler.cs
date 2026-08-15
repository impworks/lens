using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lens.LanguageServer.Core;
using Lens.LanguageServer.Protocol;
using MediatR;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

// 'Diagnostic' is also the name of the compiler's own type, and this assembly sits inside the Lens
// namespace, so the protocol's has to be spelled out
using LspDiagnostic = OmniSharp.Extensions.LanguageServer.Protocol.Models.Diagnostic;

namespace Lens.LanguageServer.Handlers
{
    /// <summary>
    /// Keeps the server's copy of each open file in step with the editor's, and publishes what is
    /// wrong with it every time it changes.
    ///
    /// The whole file is sent on every change rather than a delta. LENS scripts are small and the
    /// compiler re-reads one in milliseconds, so incremental sync would buy nothing and cost the
    /// correctness of every position in the file.
    /// </summary>
    internal sealed class DocumentHandler : TextDocumentSyncHandlerBase
    {
        #region Constructor

        public DocumentHandler(LensLanguageService service, ILanguageServerFacade facade)
        {
            _service = service;
            _facade = facade;
        }

        #endregion

        #region Fields

        private readonly LensLanguageService _service;
        private readonly ILanguageServerFacade _facade;

        #endregion

        #region Handlers

        public override Task<Unit> Handle(DidOpenTextDocumentParams request, CancellationToken cancellationToken)
        {
            _service.Open(request.TextDocument.Uri.ToString(), request.TextDocument.Text, request.TextDocument.Version ?? 0);
            Publish(request.TextDocument.Uri);

            return Unit.Task;
        }

        public override Task<Unit> Handle(DidChangeTextDocumentParams request, CancellationToken cancellationToken)
        {
            var text = request.ContentChanges.LastOrDefault()?.Text;
            if (text != null)
            {
                _service.Change(request.TextDocument.Uri.ToString(), text, request.TextDocument.Version ?? 0);
                Publish(request.TextDocument.Uri);
            }

            return Unit.Task;
        }

        public override Task<Unit> Handle(DidCloseTextDocumentParams request, CancellationToken cancellationToken)
        {
            _service.Close(request.TextDocument.Uri.ToString());

            // an editor keeps showing the last diagnostics until they are cleared
            _facade.TextDocument.PublishDiagnostics(
                new PublishDiagnosticsParams
                {
                    Uri = request.TextDocument.Uri,
                    Diagnostics = new Container<LspDiagnostic>()
                }
            );

            return Unit.Task;
        }

        public override Task<Unit> Handle(DidSaveTextDocumentParams request, CancellationToken cancellationToken)
        {
            Publish(request.TextDocument.Uri);
            return Unit.Task;
        }

        #endregion

        #region Registration

        public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri)
        {
            return new TextDocumentAttributes(uri, LensLanguage.Id);
        }

        protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability capability, ClientCapabilities clientCapabilities)
        {
            return new TextDocumentSyncRegistrationOptions
            {
                DocumentSelector = LensLanguage.Selector,
                Change = TextDocumentSyncKind.Full,
                Save = new SaveOptions {IncludeText = false}
            };
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Sends the current problems for a file to the editor.
        /// </summary>
        private void Publish(DocumentUri uri)
        {
            var problems = _service.Diagnose(uri.ToString())
                                   .Select(
                                       x => new LspDiagnostic
                                       {
                                           Message = x.Message,
                                           Severity = Conversions.ToSeverity(x.Severity),
                                           Range = Conversions.ToRange(x.Range),
                                           Source = LensLanguage.Id
                                       }
                                   )
                                   .ToArray();

            _facade.TextDocument.PublishDiagnostics(
                new PublishDiagnosticsParams
                {
                    Uri = uri,
                    Diagnostics = new Container<LspDiagnostic>(problems)
                }
            );
        }

        #endregion
    }
}
