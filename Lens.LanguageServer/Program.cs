using System;
using System.Threading.Tasks;
using Lens.LanguageServer.Handlers;
using Lens.LanguageServer.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Server;

namespace Lens.LanguageServer
{
    /// <summary>
    /// The LENS language server.
    ///
    /// Speaks the language server protocol over standard input and output, which is what every
    /// editor that supports the protocol expects to launch. Everything it answers comes from
    /// Lens.LanguageServer.Core, which knows nothing about the protocol - so an editor that would
    /// rather host the language services in-process can skip this executable entirely.
    /// </summary>
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // stdout carries the protocol; anything written to it that is not a message breaks the
            // connection, so diagnostics of our own go to stderr
            var logToStandardError = Array.IndexOf(args, "--verbose") >= 0;

            using (var service = new LensLanguageService())
            {
                var server = await OmniSharp.Extensions.LanguageServer.Server.LanguageServer.From(
                    options => options
                               .WithInput(Console.OpenStandardInput())
                               .WithOutput(Console.OpenStandardOutput())
                               .ConfigureLogging(
                                   x => x
                                        .AddLanguageProtocolLogging()
                                        .SetMinimumLevel(logToStandardError ? LogLevel.Debug : LogLevel.Warning)
                               )
                               .WithServices(x => x.AddSingleton(service))
                               .OnInitialize(
                                   (_, request, _) =>
                                       {
                                           StaticCapabilities.Apply(request.Capabilities);
                                           return Task.CompletedTask;
                                       }
                               )
                               .WithHandler<DocumentHandler>()
                               .WithHandler<CompletionHandler>()
                               .WithHandler<LensHoverHandler>()
                               .WithHandler<LensDefinitionHandler>()
                               .WithHandler<LensReferencesHandler>()
                               .WithHandler<LensDocumentSymbolHandler>()
                               .WithHandler<LensRenameHandler>()
                               .WithHandler<LensSemanticTokensHandler>()
                );

                await server.WaitForExit;
            }
        }
    }
}
