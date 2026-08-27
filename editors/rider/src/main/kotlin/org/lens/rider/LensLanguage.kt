package org.lens.rider

import com.intellij.lang.Language

/**
 * The LENS language.
 *
 * Rider decides what a file is by its language rather than by its extension, and several things
 * this plugin needs - the breakpoint gutter above all - are keyed on a registered language, so a
 * language of our own has to exist even though the parsing happens in the language server.
 */
object LensLanguage : Language("LENS") {

    /**
     * The identifier the language server matches its document selector against.
     */
    const val LSP_ID = "lens"

    override fun isCaseSensitive() = true

    override fun getDisplayName() = "LENS"

    private fun readResolve(): Any = LensLanguage
}
