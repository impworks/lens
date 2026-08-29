package org.lens.rider

import com.intellij.lexer.LexerBase
import com.intellij.psi.TokenType
import com.intellij.psi.tree.IElementType

/**
 * A lexer that knows just enough LENS to colour a script.
 *
 * The language server colours names properly - it is the only thing that can tell a record from a
 * local - but it can only do so once it has parsed the file, and it reports nothing for comments.
 * This covers the lexical half, so a freshly opened file is readable straight away, and it mirrors
 * the TextMate grammar the VS Code extension uses so that the two editors agree.
 *
 * Every token is scanned from its own start, so the lexer needs no state and the platform is free
 * to restart it at any token boundary.
 */
class LensLexer : LexerBase() {

    private var buffer: CharSequence = ""
    private var bufferEnd = 0
    private var tokenStart = 0
    private var tokenEnd = 0
    private var tokenType: IElementType? = null

    override fun start(buffer: CharSequence, startOffset: Int, endOffset: Int, initialState: Int) {
        this.buffer = buffer
        this.bufferEnd = endOffset
        this.tokenStart = startOffset
        this.tokenEnd = startOffset

        advance()
    }

    override fun getState() = 0

    override fun getTokenType() = tokenType

    override fun getTokenStart() = tokenStart

    override fun getTokenEnd() = tokenEnd

    override fun getBufferSequence() = buffer

    override fun getBufferEnd() = bufferEnd

    override fun advance() {
        tokenStart = tokenEnd

        if (tokenStart >= bufferEnd) {
            tokenType = null
            return
        }

        tokenType = scan()
    }

    /**
     * Reads one token starting at the current position and returns its kind, leaving tokenEnd just
     * past it.
     */
    private fun scan(): IElementType {
        val ch = buffer[tokenStart]

        if (ch.isWhitespace())
            return take(TokenType.WHITE_SPACE) { it.isWhitespace() }

        if (ch == '/' && peek(1) == '/')
            return comment()

        if (ch == '$' && (peek(1) == '"' || (peek(1) == '@' && peek(2) == '"')))
            return interpolatedString()

        if (ch == '@' && peek(1) == '"')
            return verbatimString(tokenStart + 2)

        if (ch == '"')
            return escapedString(tokenStart + 1, '"')

        if (ch == '\'')
            return escapedString(tokenStart + 1, '\'', LensTokenTypes.CHARACTER)

        if (ch == '#')
            return regex()

        if (ch.isDigit())
            return number()

        if (ch.isLetter() || ch == '_')
            return word()

        BRACKETS[ch]?.let {
            tokenEnd = tokenStart + 1
            return it
        }

        return operator()
    }

    private fun comment(): IElementType {
        var end = tokenStart + 2
        while (end < bufferEnd && buffer[end] != '\n' && buffer[end] != '\r')
            end++

        tokenEnd = end
        return LensTokenTypes.COMMENT
    }

    /**
     * An interpolated string. Its holes are left inside the string token: colouring code inside a
     * hole would need the lexer to nest, and the language server reports the hole contents anyway.
     */
    private fun interpolatedString(): IElementType {
        val verbatim = peek(1) == '@'
        val from = tokenStart + (if (verbatim) 3 else 2)

        return if (verbatim) verbatimString(from) else escapedString(from, '"')
    }

    /**
     * A string that ends at the first unescaped quote, where the escape is a backslash.
     */
    private fun escapedString(from: Int, quote: Char, type: IElementType = LensTokenTypes.STRING): IElementType {
        var end = from

        while (end < bufferEnd) {
            val curr = buffer[end]

            // an unterminated literal must not swallow the rest of the file
            if (curr == '\n')
                break

            if (curr == '\\' && end + 1 < bufferEnd) {
                end += 2
                continue
            }

            end++

            if (curr == quote)
                break
        }

        tokenEnd = end
        return type
    }

    /**
     * A verbatim string, where a doubled quote is the only escape and newlines are allowed.
     */
    private fun verbatimString(from: Int): IElementType {
        var end = from

        while (end < bufferEnd) {
            if (buffer[end] == '"') {
                if (peekAt(end + 1) == '"') {
                    end += 2
                    continue
                }

                end++
                break
            }

            end++
        }

        tokenEnd = end
        return LensTokenTypes.STRING
    }

    /**
     * A regex literal, delimited by hashes with a doubled hash standing for a literal one, and
     * followed by option letters.
     */
    private fun regex(): IElementType {
        var end = tokenStart + 1

        while (end < bufferEnd) {
            if (buffer[end] == '#') {
                if (peekAt(end + 1) == '#') {
                    end += 2
                    continue
                }

                end++

                while (end < bufferEnd && buffer[end].isLetter())
                    end++

                tokenEnd = end
                return LensTokenTypes.REGEX
            }

            end++
        }

        // a lone hash is not a LENS operator, so an unterminated literal is better shown as an error
        tokenEnd = tokenStart + 1
        return TokenType.BAD_CHARACTER
    }

    private fun number(): IElementType {
        var end = tokenStart

        while (end < bufferEnd && buffer[end].isDigit())
            end++

        if (end < bufferEnd && buffer[end] == '.' && peekAt(end + 1)?.isDigit() == true) {
            end++
            while (end < bufferEnd && buffer[end].isDigit())
                end++
        }

        if (end < bufferEnd && buffer[end] in NUMBER_SUFFIXES)
            end++

        tokenEnd = end
        return LensTokenTypes.NUMBER
    }

    private fun word(): IElementType {
        var end = tokenStart

        while (end < bufferEnd && (buffer[end].isLetterOrDigit() || buffer[end] == '_'))
            end++

        tokenEnd = end

        val text = buffer.subSequence(tokenStart, end).toString()

        return when {
            text in LensTokenTypes.CONSTANTS -> LensTokenTypes.CONSTANT
            text in LensTokenTypes.KEYWORDS -> LensTokenTypes.KEYWORD
            else -> LensTokenTypes.IDENTIFIER
        }
    }

    private fun operator(): IElementType {
        for (length in MAX_OPERATOR_LENGTH downTo 1) {
            if (tokenStart + length > bufferEnd)
                continue

            if (buffer.subSequence(tokenStart, tokenStart + length).toString() in OPERATORS) {
                tokenEnd = tokenStart + length
                return LensTokenTypes.OPERATOR
            }
        }

        tokenEnd = tokenStart + 1
        return TokenType.BAD_CHARACTER
    }

    private inline fun take(type: IElementType, accept: (Char) -> Boolean): IElementType {
        var end = tokenStart
        while (end < bufferEnd && accept(buffer[end]))
            end++

        tokenEnd = end
        return type
    }

    private fun peek(offset: Int) = peekAt(tokenStart + offset)

    private fun peekAt(index: Int) = if (index < bufferEnd) buffer[index] else null

    companion object {

        private const val NUMBER_SUFFIXES = "FfMmLl"

        private val BRACKETS = mapOf(
            '{' to LensTokenTypes.LEFT_BRACE,
            '}' to LensTokenTypes.RIGHT_BRACE,
            '[' to LensTokenTypes.LEFT_BRACKET,
            ']' to LensTokenTypes.RIGHT_BRACKET,
            '(' to LensTokenTypes.LEFT_PARENTHESIS,
            ')' to LensTokenTypes.RIGHT_PARENTHESIS
        )

        /**
         * The operators of the TextMate grammar, longest match first.
         */
        private val OPERATORS = setOf(
            "...", "**", "<:", ":>", "==", "<=", ">=", "<>", "??", "?.", "?[", "&&", "||", "^^",
            "::", "..", "->", "=>", "|>", "<|",
            "+", "-", "*", "/", "%", "&", "|", "^", "<", ">", "=", "~", "?", ":", ".", ",", ";"
        )

        private val MAX_OPERATOR_LENGTH = OPERATORS.maxOf { it.length }
    }
}
