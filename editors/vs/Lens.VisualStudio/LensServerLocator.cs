using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Lens.VisualStudio
{
    /// <summary>
    /// Works out how to start the LENS language server.
    ///
    /// The extension normally ships the server inside itself, but a developer working on the server
    /// wants the build they just made instead - hence the environment variable, which is the only
    /// override that can be read this early. Visual Studio settings arrive over the protocol, which
    /// is too late to decide what to launch.
    /// </summary>
    internal static class LensServerLocator
    {
        /// <summary>
        /// Path to a lens-language-server.dll or to a self-contained executable, overriding the
        /// copy bundled with the extension.
        /// </summary>
        public const string OverrideVariable = "LENS_LANGUAGE_SERVER";

        /// <summary>
        /// The dotnet host used to run the server when it is a .dll.
        /// </summary>
        public const string DotnetVariable = "LENS_LANGUAGE_SERVER_DOTNET";

        /// <summary>
        /// Builds the process description for the server, or throws with a message the user can act
        /// on - Visual Studio shows whatever is thrown here in an info bar.
        /// </summary>
        public static ProcessStartInfo Resolve()
        {
            var server = Override() ?? Bundled();

            if (server == null)
            {
                throw new FileNotFoundException(
                    "The LENS language server was not found. Build it with \"dotnet publish " +
                    "Lens.LanguageServer\" and point " + OverrideVariable + " at the result, or " +
                    "reinstall the extension with the server bundled."
                );
            }

            var info = new ProcessStartInfo
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(server)
            };

            if (server.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                info.FileName = Environment.GetEnvironmentVariable(DotnetVariable) ?? "dotnet";
                info.Arguments = Quote(server);
            }
            else
            {
                info.FileName = server;
            }

            return info;
        }

        private static string Override()
        {
            var configured = Environment.GetEnvironmentVariable(OverrideVariable);

            return string.IsNullOrWhiteSpace(configured) ? null : Existing(configured.Trim());
        }

        /// <summary>
        /// The server shipped inside the extension. The apphost is preferred over the .dll because
        /// it does not depend on dotnet being on the PATH of the Visual Studio process.
        /// </summary>
        private static string Bundled()
        {
            var root = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (root == null)
                return null;

            return Existing(Path.Combine(root, "server", "lens-language-server.exe"))
                   ?? Existing(Path.Combine(root, "server", "lens-language-server.dll"));
        }

        private static string Existing(string path)
        {
            return File.Exists(path) ? path : null;
        }

        private static string Quote(string value)
        {
            return "\"" + value + "\"";
        }
    }
}
