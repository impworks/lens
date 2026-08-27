package org.lens.rider

import com.intellij.testFramework.ParsingTestCase

/**
 * The one property the breakpoint gutter depends on.
 *
 * Rider asks PsiManager for the file and then looks up a debugger support policy by the language of
 * that PsiFile. Without a parser definition the platform hands back a plain text file, whose
 * language is PlainText, the lookup finds nothing and no breakpoint can be set - which is the state
 * this plugin exists to fix, so it is worth asserting.
 */
class LensPsiTest : ParsingTestCase("", LensFileType.EXTENSION, LensParserDefinition()) {

    override fun getTestDataPath() = "src/test/testData"

    fun testAScriptBecomesALensFile() {
        val file = createPsiFile("sample", "let x = 1\n")

        assertTrue("the file has to be a LENS PSI file, not a plain text one", file is LensFile)
        assertEquals(LensLanguage, file.language)
    }

    fun testTheTreeCoversTheWholeFile() {
        val text = "// a comment\nvar y = \"text\"\n"
        val file = createPsiFile("sample", text)

        assertEquals(text, file.node.text)
    }
}
