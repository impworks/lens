using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

namespace Lens.Playground
{
    /// <summary>
    /// Starts the compiler side of the playground.
    ///
    /// There are no components: the page is plain HTML around a Monaco editor, and this half of
    /// the application exists only to answer it. Blazor is here for its runtime and its JavaScript
    /// bridge, not for its rendering.
    /// </summary>
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            // before anything else can touch Console.In, which throws on this platform unless
            // something has been put there first
            PlaygroundConsole.Install();

            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            var host = builder.Build();

            Interop.Initialize((IJSInProcessRuntime) host.Services.GetService(typeof(IJSRuntime)));
            Interop.WarmUp();

            // tells the page the compiler is ready, which is when it stops showing the loader
            ((IJSInProcessRuntime) host.Services.GetService(typeof(IJSRuntime))).InvokeVoid("lensPlayground.onReady");

            await host.RunAsync();
        }
    }
}
