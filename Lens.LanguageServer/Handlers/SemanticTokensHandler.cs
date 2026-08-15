using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lens.LanguageServer.Core;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Lens.LanguageServer.Handlers
{
    /// <summary>
    /// Colouring, from the compiler rather than from a regular expression.
    ///
    /// The editor's own grammar already knows a keyword from a number. What it cannot know is that
    /// a bare name is a record, an argument, or a function the host registered - and that is most
    /// of a LENS script.
    /// </summary>
    internal sealed class LensSemanticTokensHandler : SemanticTokensHandlerBase
    {
        #region Constructor

        public LensSemanticTokensHandler(LensLanguageService service)
        {
            _service = service;
        }

        #endregion

        #region Fields

        private readonly LensLanguageService _service;

        #endregion

        #region Handlers

        protected override Task Tokenize(SemanticTokensBuilder builder, ITextDocumentIdentifierParams identifier, CancellationToken cancellationToken)
        {
            foreach (var curr in _service.Colour(identifier.TextDocument.Uri.ToString()))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                builder.Push(curr.Line, curr.Character, curr.Length, curr.TokenType, 0);
            }

            return Task.CompletedTask;
        }

        protected override Task<SemanticTokensDocument> GetSemanticTokensDocument(ITextDocumentIdentifierParams @params, CancellationToken cancellationToken)
        {
            return Task.FromResult(new SemanticTokensDocument(RegistrationOptions.Legend));
        }

        #endregion

        #region Registration

        protected override SemanticTokensRegistrationOptions CreateRegistrationOptions(SemanticTokensCapability capability, ClientCapabilities clientCapabilities)
        {
            return new SemanticTokensRegistrationOptions
            {
                DocumentSelector = LensLanguage.Selector,
                Legend = new SemanticTokensLegend
                {
                    TokenTypes = new Container<SemanticTokenType>(SemanticTokenLegend.TokenTypes.Select(x => new SemanticTokenType(x))),
                    TokenModifiers = new Container<SemanticTokenModifier>(SemanticTokenLegend.TokenModifiers.Select(x => new SemanticTokenModifier(x)))
                },
                Full = new SemanticTokensCapabilityRequestFull {Delta = false},
                Range = false
            };
        }

        #endregion
    }
}
