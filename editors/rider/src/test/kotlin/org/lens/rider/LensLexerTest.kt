package org.lens.rider

import com.intellij.psi.TokenType
import com.intellij.psi.tree.IElementType
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * The lexer is the only piece of language logic in the plugin, so it is the only piece that can be
 * got wrong without the language server noticing.
 */
class LensLexerTest {

    @Test
    fun `keywords and names are told apart`() {
        assertTokens(
            "let x = 1",
            LensTokenTypes.KEYWORD to "let",
            LensTokenTypes.IDENTIFIER to "x",
            LensTokenTypes.OPERATOR to "=",
            LensTokenTypes.NUMBER to "1"
        )
    }

    @Test
    fun `a comment runs to the end of the line only`() {
        assertTokens(
            "// note\nvar",
            LensTokenTypes.COMMENT to "// note",
            LensTokenTypes.KEYWORD to "var"
        )
    }

    @Test
    fun `a string keeps its escapes`() {
        assertTokens(
            """"a\"b" 2""",
            LensTokenTypes.STRING to """"a\"b"""",
            LensTokenTypes.NUMBER to "2"
        )
    }

    @Test
    fun `a verbatim string spans lines`() {
        assertTokens(
            "@\"one\ntwo\"\n1",
            LensTokenTypes.STRING to "@\"one\ntwo\"",
            LensTokenTypes.NUMBER to "1"
        )
    }

    @Test
    fun `an interpolated string is one token`() {
        assertTokens(
            """$"hi {name}"""",
            LensTokenTypes.STRING to """$"hi {name}""""
        )
    }

    @Test
    fun `an unterminated string stops at the line end`() {
        assertTokens(
            "\"open\nlet",
            LensTokenTypes.STRING to "\"open",
            LensTokenTypes.KEYWORD to "let"
        )
    }

    @Test
    fun `a regex literal takes its options`() {
        assertTokens(
            "#a.b#ig 1",
            LensTokenTypes.REGEX to "#a.b#ig",
            LensTokenTypes.NUMBER to "1"
        )
    }

    @Test
    fun `numbers keep their suffix`() {
        assertTokens(
            "1.5f 42L",
            LensTokenTypes.NUMBER to "1.5f",
            LensTokenTypes.NUMBER to "42L"
        )
    }

    @Test
    fun `the longest operator wins`() {
        assertTokens(
            "a **b ?? c ->",
            LensTokenTypes.IDENTIFIER to "a",
            LensTokenTypes.OPERATOR to "**",
            LensTokenTypes.IDENTIFIER to "b",
            LensTokenTypes.OPERATOR to "??",
            LensTokenTypes.IDENTIFIER to "c",
            LensTokenTypes.OPERATOR to "->"
        )
    }

    @Test
    fun `brackets are told apart by side`() {
        assertTokens(
            "( ) { } [ ]",
            LensTokenTypes.LEFT_PARENTHESIS to "(",
            LensTokenTypes.RIGHT_PARENTHESIS to ")",
            LensTokenTypes.LEFT_BRACE to "{",
            LensTokenTypes.RIGHT_BRACE to "}",
            LensTokenTypes.LEFT_BRACKET to "[",
            LensTokenTypes.RIGHT_BRACKET to "]"
        )
    }

    @Test
    fun `the whole buffer is consumed`() {
        val text = listOf(
            "use System",
            "record Point",
            "    X : int",
            "",
            "fun sum:int (a:int b:int) -> a + b",
            "",
            "var p = new Point X = 1",
            "println \"{0}\" (sum 1 2) // done",
            "#\\d+#i",
            "@\"verbatim \"\"quoted\"\"\"",
            "let ünicöde = 'c'"
        ).joinToString("\n")

        val lexer = LensLexer()
        lexer.start(text)

        var offset = 0
        while (lexer.tokenType != null) {
            assertEquals("the lexer skipped a character", offset, lexer.tokenStart)
            offset = lexer.tokenEnd
            lexer.advance()
        }

        assertEquals("the lexer stopped early", text.length, offset)
    }

    /**
     * Compares the tokens of a snippet, ignoring whitespace.
     */
    private fun assertTokens(text: String, vararg expected: Pair<IElementType, String>) {
        val lexer = LensLexer()
        lexer.start(text)

        val actual = mutableListOf<Pair<IElementType, String>>()
        while (lexer.tokenType != null) {
            val type = lexer.tokenType!!
            if (type != TokenType.WHITE_SPACE)
                actual.add(type to text.substring(lexer.tokenStart, lexer.tokenEnd))

            lexer.advance()
        }

        assertEquals(expected.toList(), actual)
    }
}
