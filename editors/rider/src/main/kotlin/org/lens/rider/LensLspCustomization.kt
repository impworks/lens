package org.lens.rider

import com.intellij.platform.lsp.api.customization.LspCustomization
import com.intellij.platform.lsp.api.customization.LspSemanticTokensCustomizer
import com.intellij.platform.lsp.api.customization.LspSemanticTokensSupport
import com.intellij.psi.PsiFile

/**
 * Asks the server to colour LENS files.
 *
 * Semantic tokens are what colour a type, a function, a parameter or a field: the lexer here knows
 * only the shapes a regular expression can find, which is keywords, strings, numbers and comments.
 * Everything else needs the compiler, and the compiler answers over the protocol.
 *
 * The platform enables semantic tokens by default and then asks for them only where the file has no
 * language of its own - its default answer is "yes" for plain text and for a TextMate file, and "no"
 * for everything else, on the reasonable assumption that a registered language brings its own
 * colouring. Ours does not, and cannot: a language had to be registered for breakpoints to be
 * offered at all, and registering one is what turned the colouring off.
 */
class LensLspCustomization : LspCustomization() {

    override val semanticTokensCustomizer: LspSemanticTokensCustomizer = LensSemanticTokens()
}

/**
 * Semantic tokens for files that belong to this plugin.
 */
class LensSemanticTokens : LspSemanticTokensSupport() {

    override fun shouldAskServerForSemanticTokens(psiFile: PsiFile): Boolean =
        psiFile.language == LensLanguage
}
