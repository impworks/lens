using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.LanguageServer.Client;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;
using Task = System.Threading.Tasks.Task;

namespace Lens.VisualStudio
{
    /// <summary>
    /// Connects Visual Studio to the LENS language server.
    ///
    /// Everything the extension offers - completion, diagnostics, hover, navigation, rename and the
    /// outline - comes from the server over stdio, exactly as it does in VS Code. Nothing here
    /// knows anything about the language itself.
    /// </summary>
    [Export(typeof(ILanguageClient))]
    [ContentType(LensContentDefinition.ContentTypeName)]
    public sealed class LensLanguageClient : ILanguageClient
    {
        public string Name => "LENS Language Server";

        /// <summary>
        /// The prefix of the settings in LensSettings.json, which is how the server is told to
        /// trace its traffic.
        /// </summary>
        public IEnumerable<string> ConfigurationSections
        {
            get { yield return "lens"; }
        }

        public object InitializationOptions => null;


        /// <summary>
        /// The server keeps its own view of the workspace, so it wants to hear about scripts that
        /// changed outside the editor.
        /// </summary>
        public IEnumerable<string> FilesToWatch
        {
            get { yield return "**/*.lns"; }
        }

        /// <summary>
        /// A missing server is a configuration problem the user has to see, not something to fail
        /// silently over.
        /// </summary>
        public bool ShowNotificationOnInitializeFailed => true;

        public event AsyncEventHandler<EventArgs> StartAsync;

        // the interface requires it, but stopping the server is Visual Studio's business - it closes
        // the streams, which the server takes as its signal to exit
#pragma warning disable 0067
        public event AsyncEventHandler<EventArgs> StopAsync;
#pragma warning restore 0067

        public async Task<Connection> ActivateAsync(CancellationToken token)
        {
            await Task.Yield();

            var info = LensServerLocator.Resolve();

            // stderr is left alone deliberately: the server logs there, and redirecting a stream
            // nobody drains eventually blocks the process once its buffer fills
            var process = new Process { StartInfo = info };

            if (!process.Start())
                throw new InvalidOperationException("The LENS language server could not be started: " + info.FileName);

            return new Connection(process.StandardOutput.BaseStream, process.StandardInput.BaseStream);
        }

        public async Task OnLoadedAsync()
        {
            var start = StartAsync;

            if (start != null)
                await start.InvokeAsync(this, EventArgs.Empty);
        }

        public Task<InitializationFailureContext> OnServerInitializeFailedAsync(ILanguageClientInitializationInfo state)
        {
            return Task.FromResult(
                new InitializationFailureContext
                {
                    FailureMessage = "The LENS language server did not start. " +
                                     (state?.InitializationException?.Message ?? string.Empty)
                }
            );
        }

        public Task OnServerInitializedAsync()
        {
            return Task.CompletedTask;
        }
    }
}
