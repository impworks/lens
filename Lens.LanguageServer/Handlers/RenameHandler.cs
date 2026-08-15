using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lens.LanguageServer.Core;
using Lens.LanguageServer.Protocol;
using OmniSharp.Extensions.JsonRpc.Server;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Lens.LanguageServer.Handlers
{
    /// <summary>
    /// Renaming a name everywhere it appears.
    ///
    /// The interesting half is refusing. The language services decide what may be renamed - a local
    /// yes, a .NET member no, anything at all while the file does not parse no - and a refusal is
    /// reported as an error the editor shows, rather than as an empty edit that looks like success.
    /// </summary>
    internal sealed class LensRenameHandler : RenameHandlerBase
    {
        #region Constructor

        public LensRenameHandler(LensLanguageService service)
        {
            _service = service;
        }

        #endregion

        #region Fields

        private readonly LensLanguageService _service;

        #endregion

        #region Handlers

        public override Task<WorkspaceEdit> Handle(RenameParams request, CancellationToken cancellationToken)
        {
            var uri = request.TextDocument.Uri;
            var outcome = _service.Rename(uri.ToString(), Conversions.ToPosition(request.Position), request.NewName);

            if (!outcome.IsAllowed)
                throw new RequestCancelledException(outcome.Refusal);

            var edits = outcome.Edits
                               .Select(x => new TextEdit {Range = Conversions.ToRange(x.Range), NewText = x.Text})
                               .ToArray();

            var result = new WorkspaceEdit
            {
                Changes = new Dictionary<DocumentUri, IEnumerable<TextEdit>> {{uri, edits}}
            };

            return Task.FromResult(result);
        }

        #endregion

        #region Registration

        protected override RenameRegistrationOptions CreateRegistrationOptions(RenameCapability capability, ClientCapabilities clientCapabilities)
        {
            return new RenameRegistrationOptions
            {
                DocumentSelector = LensLanguage.Selector,
                PrepareProvider = false
            };
        }

        #endregion
    }
}
