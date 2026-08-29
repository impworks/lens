package org.lens.rider

import com.intellij.execution.configurations.GeneralCommandLine
import com.intellij.openapi.application.PathManager
import com.intellij.openapi.diagnostic.logger
import java.net.URI
import java.nio.file.Files
import java.nio.file.Path

/**
 * Finds the language server executable.
 *
 * The order matches the VS Code extension: an explicit setting wins, then the environment, then the
 * copy that was packaged with the plugin. A path that was configured but does not exist falls
 * through to the next candidate rather than ending the search - a stale setting left over from a
 * moved build should not leave the plugin with no server at all.
 */
object LensServerLocator {

    private val log = logger<LensServerLocator>()

    /**
     * The directory the plugin is unpacked into, which is the name of the distribution.
     */
    private const val PLUGIN_DIRECTORY = "LensLang-Rider"

    private const val ENVIRONMENT_VARIABLE = "LENS_LANGUAGE_SERVER"

    private val NAMES = listOf(
        "lens-language-server.dll",
        "lens-language-server.exe",
        "lens-language-server"
    )

    /**
     * The server to launch, or null when none was found.
     */
    fun locate(): Path? {
        val located = configured() ?: fromEnvironment() ?: bundled()

        if (located == null)
            log.warn("No LENS language server was found. Set its path in Settings | Tools | LENS.")

        return located
    }

    /**
     * Builds the command line for a located server: a self-contained executable runs on its own, a
     * .dll needs the dotnet host.
     */
    fun commandLine(server: Path): GeneralCommandLine {
        val command =
            if (server.fileName.toString().endsWith(".dll"))
                GeneralCommandLine(LensSettings.getInstance().dotnetPath, server.toString())
            else
                GeneralCommandLine(server.toString())

        return command
            .withWorkDirectory(server.parent?.toString())
            .withCharset(Charsets.UTF_8)
    }

    private fun configured(): Path? {
        val path = LensSettings.getInstance().serverPath.trim()
        if (path.isEmpty())
            return null

        val file = Path.of(path)

        if (!Files.isRegularFile(file)) {
            log.warn("The LENS language server configured in settings does not exist: $file")
            return null
        }

        return file
    }

    private fun fromEnvironment(): Path? {
        val path = System.getenv(ENVIRONMENT_VARIABLE)?.trim().orEmpty()
        if (path.isEmpty())
            return null

        val file = Path.of(path)

        if (!Files.isRegularFile(file)) {
            log.warn("$ENVIRONMENT_VARIABLE points at a file that does not exist: $file")
            return null
        }

        return file
    }

    /**
     * The copy packaged with the plugin, in the "server" directory beside "lib".
     *
     * The plugin is found through the jar this class was loaded from. The location of that jar is
     * handed out as a URL, and for a class inside a jar it is a "jar:file:...!/" URL, which is not
     * a file path and cannot be turned into one directly - reading it as though it were is what
     * used to leave the plugin unable to find its own server while reporting no reason at all.
     *
     * The plugins directory is tried as well, because a jar is not the only shape a plugin can be
     * loaded from: run straight out of a build, the classes come from a directory instead.
     */
    private fun bundled(): Path? {
        val roots = listOfNotNull(fromJar(), PathManager.getPluginsDir().resolve(PLUGIN_DIRECTORY))

        for (root in roots) {
            val directory = root.resolve("server")
            val server = NAMES.map { directory.resolve(it) }.firstOrNull { Files.isRegularFile(it) }

            if (server != null)
                return server
        }

        log.warn("No language server is bundled with the plugin. Looked under: " + roots.joinToString())

        return null
    }

    /**
     * The directory holding the plugin, worked out from the jar this class was loaded from: the jar
     * sits in "lib", and the plugin directory is its parent.
     */
    private fun fromJar(): Path? {
        val location = LensServerLocator::class.java.protectionDomain?.codeSource?.location ?: return null

        // "jar:file:/x/lib/plugin.jar!/" names an entry inside the jar; the jar itself is what is
        // wanted, and a plain "file:" URL is already that
        val jar = location.toString().removePrefix("jar:").substringBefore("!/")

        return runCatching { Path.of(URI(jar)).parent?.parent }.getOrNull()
    }
}
