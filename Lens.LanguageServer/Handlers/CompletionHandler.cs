using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lens.LanguageServer.Core;
using Lens.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Lens.LanguageServer.Handlers
{
    /// <summary>
    /// Offers the names that may be written where the caret is.
    ///
    /// '.' and ':' are trigger characters - the second because '::' reaches a static member and
    /// arrives one keystroke at a time, so the trigger has to be the character it ends with. '?' is
    /// not, because '?.' ends in a dot and triggers anyway.
    /// </summary>
    internal sealed class CompletionHandler : CompletionHandlerBase
    {
        #region Constructor

        public CompletionHandler(LensLanguageService service)
        {
            _service = service;
        }

        #endregion

        #region Fields

        private readonly LensLanguageService _service;

        #endregion

        #region Handlers

        public override Task<CompletionList> Handle(CompletionParams request, CancellationToken cancellationToken)
        {
            var items = _service
                        .Suggest(request.TextDocument.Uri.ToString(), Conversions.ToPosition(request.Position))
                        .Select(
                            x => new CompletionItem
                            {
                                Label = x.Label,
                                Kind = Conversions.ToCompletionKind(x.Kind),
                                Detail = x.Detail,

                                // members first, then locals, then everything the environment
                                // offers - which is long and rarely what is being typed
                                SortText = SortKeyOf(x) + x.Label
                            }
                        )
                        .ToArray();

            return Task.FromResult(new CompletionList(items));
        }

        /// <summary>
        /// Completion items are complete as produced; there is nothing to fill in later.
        /// </summary>
        public override Task<CompletionItem> Handle(CompletionItem request, CancellationToken cancellationToken)
        {
            return Task.FromResult(request);
        }

        #endregion

        #region Registration

        protected override CompletionRegistrationOptions CreateRegistrationOptions(CompletionCapability capability, ClientCapabilities clientCapabilities)
        {
            return new CompletionRegistrationOptions
            {
                DocumentSelector = LensLanguage.Selector,
                TriggerCharacters = new Container<string>(".", ":"),
                ResolveProvider = false
            };
        }

        #endregion

        #region Helpers

        /// <summary>
        /// What to sort a suggestion under, so that the likely answers come first.
        /// </summary>
        private static string SortKeyOf(Suggestion suggestion)
        {
            switch (suggestion.Kind)
            {
                // a 'use' directive is offered nothing but namespaces, so there is nothing for them
                // to be sorted against - and everywhere else they are not offered at all
                case Analysis.SymbolKind.Namespace:
                case Analysis.SymbolKind.Local:
                case Analysis.SymbolKind.Parameter:
                    return "1";

                case Analysis.SymbolKind.Member:
                case Analysis.SymbolKind.RecordField:
                    return "2";

                case Analysis.SymbolKind.Function:
                case Analysis.SymbolKind.GlobalVariable:
                    return "3";

                case Analysis.SymbolKind.Keyword:
                    return "5";

                default:
                    return "4";
            }
        }

        #endregion
    }
}
