package org.lens.rider

import com.intellij.lexer.Lexer
import com.intellij.openapi.editor.DefaultLanguageHighlighterColors
import com.intellij.openapi.editor.colors.TextAttributesKey
import com.intellij.openapi.fileTypes.SyntaxHighlighter
import com.intellij.openapi.fileTypes.SyntaxHighlighterBase
import com.intellij.openapi.fileTypes.SyntaxHighlighterFactory
import com.intellij.openapi.project.Project
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.psi.TokenType
import com.intellij.psi.tree.IElementType

/**
 * The colours the lexer produces.
 *
 * Everything is expressed through the default language colours, so a LENS script follows whatever
 * scheme the user picked without any of its own settings.
 */
object LensColors {

    val COMMENT: TextAttributesKey =
        TextAttributesKey.createTextAttributesKey("LENS_COMMENT", DefaultLanguageHighlighterColors.LINE_COMMENT)

    val KEYWORD: TextAttributesKey =
        TextAttributesKey.createTextAttributesKey("LENS_KEYWORD", DefaultLanguageHighlighterColors.KEYWORD)

    val CONSTANT: TextAttributesKey =
        TextAttributesKey.createTextAttributesKey("LENS_CONSTANT", DefaultLanguageHighlighterColors.CONSTANT)

    val STRING: TextAttributesKey =
        TextAttributesKey.createTextAttributesKey("LENS_STRING", DefaultLanguageHighlighterColors.STRING)

    val NUMBER: TextAttributesKey =
        TextAttributesKey.createTextAttributesKey("LENS_NUMBER", DefaultLanguageHighlighterColors.NUMBER)

    val REGEX: TextAttributesKey =
        TextAttributesKey.createTextAttributesKey("LENS_REGEX", DefaultLanguageHighlighterColors.VALID_STRING_ESCAPE)

    val IDENTIFIER: TextAttributesKey =
        TextAttributesKey.createTextAttributesKey("LENS_IDENTIFIER", DefaultLanguageHighlighterColors.IDENTIFIER)

    val OPERATOR: TextAttributesKey =
        TextAttributesKey.createTextAttributesKey("LENS_OPERATOR", DefaultLanguageHighlighterColors.OPERATION_SIGN)

    val BRACE: TextAttributesKey =
        TextAttributesKey.createTextAttributesKey("LENS_BRACE", DefaultLanguageHighlighterColors.BRACES)

    val BRACKET: TextAttributesKey =
        TextAttributesKey.createTextAttributesKey("LENS_BRACKET", DefaultLanguageHighlighterColors.BRACKETS)

    val PARENTHESIS: TextAttributesKey =
        TextAttributesKey.createTextAttributesKey("LENS_PARENTHESIS", DefaultLanguageHighlighterColors.PARENTHESES)

    val BAD_CHARACTER: TextAttributesKey =
        TextAttributesKey.createTextAttributesKey("LENS_BAD_CHARACTER", com.intellij.openapi.editor.colors.CodeInsightColors.ERRORS_ATTRIBUTES)
}

class LensSyntaxHighlighter : SyntaxHighlighterBase() {

    override fun getHighlightingLexer(): Lexer = LensLexer()

    override fun getTokenHighlights(tokenType: IElementType): Array<TextAttributesKey> {
        val key = when (tokenType) {
            LensTokenTypes.COMMENT -> LensColors.COMMENT
            LensTokenTypes.KEYWORD -> LensColors.KEYWORD
            LensTokenTypes.CONSTANT -> LensColors.CONSTANT
            LensTokenTypes.STRING, LensTokenTypes.CHARACTER -> LensColors.STRING
            LensTokenTypes.REGEX -> LensColors.REGEX
            LensTokenTypes.NUMBER -> LensColors.NUMBER
            LensTokenTypes.IDENTIFIER -> LensColors.IDENTIFIER
            LensTokenTypes.OPERATOR -> LensColors.OPERATOR
            LensTokenTypes.LEFT_BRACE, LensTokenTypes.RIGHT_BRACE -> LensColors.BRACE
            LensTokenTypes.LEFT_BRACKET, LensTokenTypes.RIGHT_BRACKET -> LensColors.BRACKET
            LensTokenTypes.LEFT_PARENTHESIS, LensTokenTypes.RIGHT_PARENTHESIS -> LensColors.PARENTHESIS
            TokenType.BAD_CHARACTER -> LensColors.BAD_CHARACTER
            else -> return emptyArray()
        }

        return arrayOf(key)
    }
}

class LensSyntaxHighlighterFactory : SyntaxHighlighterFactory() {

    override fun getSyntaxHighlighter(project: Project?, virtualFile: VirtualFile?): SyntaxHighlighter =
        LensSyntaxHighlighter()
}
