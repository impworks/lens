package org.lens.rider

import com.intellij.openapi.options.colors.AttributesDescriptor
import com.intellij.openapi.options.colors.ColorDescriptor
import com.intellij.openapi.options.colors.ColorSettingsPage

/**
 * Lets the lexical colours be edited under Editor | Color Scheme, which is also where the platform
 * looks for a preview of a language.
 */
class LensColorSettingsPage : ColorSettingsPage {

    override fun getDisplayName() = "LENS"

    override fun getIcon() = LensFileType.getIcon()

    override fun getAttributeDescriptors() = DESCRIPTORS

    override fun getColorDescriptors(): Array<ColorDescriptor> = ColorDescriptor.EMPTY_ARRAY

    override fun getHighlighter() = LensSyntaxHighlighter()

    override fun getAdditionalHighlightingTagToDescriptorMap(): Map<String, com.intellij.openapi.editor.colors.TextAttributesKey>? = null

    override fun getDemoText() = """
        // a script the host can compile and debug
        use System.Linq

        record Point
            X : int
            Y : int

        fun distance:double (a:Point b:Point) ->
            let dx = a.X - b.X
            let dy = a.Y - b.Y
            Math::Sqrt (dx ** 2 + dy ** 2)

        var origin = new Point X = 0 Y = 0
        var target = new Point X = 3 Y = 4

        if (distance origin target > 2.5) then
            println "far: {0}" (distance origin target)
        else
            println @"near"
    """.trimIndent()

    companion object {
        private val DESCRIPTORS = arrayOf(
            AttributesDescriptor("Comment", LensColors.COMMENT),
            AttributesDescriptor("Keyword", LensColors.KEYWORD),
            AttributesDescriptor("Constant", LensColors.CONSTANT),
            AttributesDescriptor("String", LensColors.STRING),
            AttributesDescriptor("Number", LensColors.NUMBER),
            AttributesDescriptor("Regular expression", LensColors.REGEX),
            AttributesDescriptor("Identifier", LensColors.IDENTIFIER),
            AttributesDescriptor("Operator", LensColors.OPERATOR),
            AttributesDescriptor("Braces", LensColors.BRACE),
            AttributesDescriptor("Brackets", LensColors.BRACKET),
            AttributesDescriptor("Parentheses", LensColors.PARENTHESIS)
        )
    }
}
