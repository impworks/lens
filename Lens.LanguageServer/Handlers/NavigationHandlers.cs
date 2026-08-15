using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lens.LanguageServer.Core;
using Lens.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Lens.LanguageServer.Handlers
{
    /// <summary>
    /// What sits under the pointer.
    /// </summary>
    internal sealed class LensHoverHandler : HoverHandlerBase
    {
        public LensHoverHandler(LensLanguageService service)
        {
            _service = service;
        }

        private readonly LensLanguageService _service;

        public override Task<Hover> Handle(HoverParams request, CancellationToken cancellationToken)
        {
            var explanation = _service.Explain(request.TextDocument.Uri.ToString(), Conversions.ToPosition(request.Position));

            if (explanation == null)
                return Task.FromResult<Hover>(null);

            var hover = new Hover
            {
                Range = Conversions.ToRange(explanation.Range),
                Contents = new MarkedStringsOrMarkupContent(
                    new MarkupContent
                    {
                        Kind = MarkupKind.Markdown,
                        Value = "```lens\n" + explanation.Text + "\n```"
                    }
                )
            };

            return Task.FromResult(hover);
        }

        protected override HoverRegistrationOptions CreateRegistrationOptions(HoverCapability capability, ClientCapabilities clientCapabilities)
        {
            return new HoverRegistrationOptions {DocumentSelector = LensLanguage.Selector};
        }
    }

    /// <summary>
    /// Where a name is declared.
    /// </summary>
    internal sealed class LensDefinitionHandler : DefinitionHandlerBase
    {
        public LensDefinitionHandler(LensLanguageService service)
        {
            _service = service;
        }

        private readonly LensLanguageService _service;

        public override Task<LocationOrLocationLinks> Handle(DefinitionParams request, CancellationToken cancellationToken)
        {
            var location = _service.Define(request.TextDocument.Uri.ToString(), Conversions.ToPosition(request.Position));

            if (location == null)
                return Task.FromResult(new LocationOrLocationLinks());

            var result = new LocationOrLocationLinks(
                new Location
                {
                    Uri = DocumentUri.Parse(location.Uri),
                    Range = Conversions.ToRange(location.Range)
                }
            );

            return Task.FromResult(result);
        }

        protected override DefinitionRegistrationOptions CreateRegistrationOptions(DefinitionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DefinitionRegistrationOptions {DocumentSelector = LensLanguage.Selector};
        }
    }

    /// <summary>
    /// Everywhere a name is written.
    /// </summary>
    internal sealed class LensReferencesHandler : ReferencesHandlerBase
    {
        public LensReferencesHandler(LensLanguageService service)
        {
            _service = service;
        }

        private readonly LensLanguageService _service;

        public override Task<LocationContainer> Handle(ReferenceParams request, CancellationToken cancellationToken)
        {
            var locations = _service
                            .FindReferences(request.TextDocument.Uri.ToString(), Conversions.ToPosition(request.Position))
                            .Select(
                                x => new Location
                                {
                                    Uri = DocumentUri.Parse(x.Uri),
                                    Range = Conversions.ToRange(x.Range)
                                }
                            )
                            .ToArray();

            return Task.FromResult(new LocationContainer(locations));
        }

        protected override ReferenceRegistrationOptions CreateRegistrationOptions(ReferenceCapability capability, ClientCapabilities clientCapabilities)
        {
            return new ReferenceRegistrationOptions {DocumentSelector = LensLanguage.Selector};
        }
    }

    /// <summary>
    /// The declarations in a file, for the outline and the breadcrumb bar.
    /// </summary>
    internal sealed class LensDocumentSymbolHandler : DocumentSymbolHandlerBase
    {
        public LensDocumentSymbolHandler(LensLanguageService service)
        {
            _service = service;
        }

        private readonly LensLanguageService _service;

        public override Task<SymbolInformationOrDocumentSymbolContainer> Handle(DocumentSymbolParams request, CancellationToken cancellationToken)
        {
            var symbols = _service
                          .Outline(request.TextDocument.Uri.ToString())
                          .Select(x => new SymbolInformationOrDocumentSymbol(Convert(x)))
                          .ToArray();

            return Task.FromResult(new SymbolInformationOrDocumentSymbolContainer(symbols));
        }

        protected override DocumentSymbolRegistrationOptions CreateRegistrationOptions(DocumentSymbolCapability capability, ClientCapabilities clientCapabilities)
        {
            return new DocumentSymbolRegistrationOptions {DocumentSelector = LensLanguage.Selector};
        }

        private static DocumentSymbol Convert(OutlineEntry entry)
        {
            return new DocumentSymbol
            {
                Name = entry.Name,
                Detail = entry.Detail,
                Kind = Conversions.ToSymbolKind(entry.Kind),
                Range = Conversions.ToRange(entry.Range),
                SelectionRange = Conversions.ToRange(entry.Selection),
                Children = new Container<DocumentSymbol>(entry.Children.Select(Convert).ToArray())
            };
        }
    }
}
