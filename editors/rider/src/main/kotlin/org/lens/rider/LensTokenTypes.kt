package org.lens.rider

import com.intellij.psi.tree.IElementType
import com.intellij.psi.tree.TokenSet

/**
 * The token kinds the lexer produces.
 *
 * They are coarse on purpose: the tree is never navigated, it only exists so that a .lns file has
 * a PSI file of the LENS language, and so that colouring works before the language server has
 * answered.
 */
class LensTokenType(debugName: String) : IElementType(debugName, LensLanguage)

object LensTokenTypes {

    val COMMENT = LensTokenType("COMMENT")
    val STRING = LensTokenType("STRING")
    val CHARACTER = LensTokenType("CHARACTER")
    val REGEX = LensTokenType("REGEX")
    val NUMBER = LensTokenType("NUMBER")
    val KEYWORD = LensTokenType("KEYWORD")
    val CONSTANT = LensTokenType("CONSTANT")
    val IDENTIFIER = LensTokenType("IDENTIFIER")
    val OPERATOR = LensTokenType("OPERATOR")
    val LEFT_BRACE = LensTokenType("LEFT_BRACE")
    val RIGHT_BRACE = LensTokenType("RIGHT_BRACE")
    val LEFT_BRACKET = LensTokenType("LEFT_BRACKET")
    val RIGHT_BRACKET = LensTokenType("RIGHT_BRACKET")
    val LEFT_PARENTHESIS = LensTokenType("LEFT_PARENTHESIS")
    val RIGHT_PARENTHESIS = LensTokenType("RIGHT_PARENTHESIS")

    val COMMENTS = TokenSet.create(COMMENT)
    val STRINGS = TokenSet.create(STRING, CHARACTER, REGEX)

    /**
     * Keywords, taken from the same list the TextMate grammar of the VS Code extension uses.
     */
    val KEYWORDS = setOf(
        "if", "then", "else", "while", "do", "for", "in", "try", "catch", "finally", "throw",
        "match", "with", "case", "when", "yield", "await", "using",
        "declare", "use", "record", "type", "fun", "pure", "let", "var",
        "new", "not", "is", "as", "of", "ref", "typeof", "default"
    )

    val CONSTANTS = setOf("true", "false", "null")
}
