package org.lens.rider

import com.intellij.execution.configurations.GeneralCommandLine
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.platform.lsp.api.LspServerSupportProvider
import com.intellij.platform.lsp.api.ProjectWideLspServerDescriptor
import com.intellij.platform.lsp.api.customization.LspCustomization

/**
 * Hands LENS files to the language server that also serves VS Code.
 *
 * Every editing feature the plugin offers beyond colouring comes from there: diagnostics,
 * completion, hover, navigation, find usages, rename and the file structure. The platform enables
 * all of them by default, so the descriptor only has to say which files are ours and how to start
 * the process.
 */
class LensLspServerSupportProvider : LspServerSupportProvider {

    override fun fileOpened(project: Project, file: VirtualFile, serverStarter: LspServerSupportProvider.LspServerStarter) {
        if (file.extension != LensFileType.EXTENSION)
            return

        // a missing server is reported by the locator and then let be: starting one that cannot be
        // found would only produce the same notification once per file opened
        if (LensServerLocator.locate() == null)
            return

        serverStarter.ensureServerStarted(LensLspServerDescriptor(project))
    }
}

class LensLspServerDescriptor(project: Project) : ProjectWideLspServerDescriptor(project, "LENS") {

    override fun isSupportedFile(file: VirtualFile) = file.extension == LensFileType.EXTENSION

    /**
     * The platform would otherwise ask for semantic tokens only for files with no language of
     * their own, and this plugin registers one - see LensLspCustomization.
     */
    override val lspCustomization: LspCustomization = LensLspCustomization()

    override fun createCommandLine(): GeneralCommandLine {
        val server = LensServerLocator.locate()
            ?: throw com.intellij.execution.ExecutionException(
                "The LENS language server was not found. Build it with \"dotnet publish Lens.LanguageServer\" " +
                "and set its path in Settings | Tools | LENS."
            )

        return LensServerLocator.commandLine(server)
    }

    /**
     * The server registers its handlers for the language id "lens" - the one the VS Code extension
     * declares - and ignores a document that arrives under any other id. The platform would derive
     * one from the file extension, which it does not know.
     */
    override fun getLanguageId(file: VirtualFile) = LensLanguage.LSP_ID
}
