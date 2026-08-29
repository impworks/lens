package org.lens.rider

import com.intellij.lang.BracePair
import com.intellij.lang.Commenter
import com.intellij.lang.PairedBraceMatcher
import com.intellij.psi.PsiFile
import com.intellij.psi.tree.IElementType

/**
 * LENS has line comments only.
 */
class LensCommenter : Commenter {

    override fun getLineCommentPrefix() = "//"

    override fun getBlockCommentPrefix(): String? = null

    override fun getBlockCommentSuffix(): String? = null

    override fun getCommentedBlockCommentPrefix(): String? = null

    override fun getCommentedBlockCommentSuffix(): String? = null
}

class LensBraceMatcher : PairedBraceMatcher {

    override fun getPairs() = PAIRS

    override fun isPairedBracesAllowedBeforeType(type: IElementType, contextType: IElementType?) = true

    override fun getCodeConstructStart(file: PsiFile?, openingBraceOffset: Int) = openingBraceOffset

    companion object {
        private val PAIRS = arrayOf(
            BracePair(LensTokenTypes.LEFT_BRACE, LensTokenTypes.RIGHT_BRACE, true),
            BracePair(LensTokenTypes.LEFT_BRACKET, LensTokenTypes.RIGHT_BRACKET, false),
            BracePair(LensTokenTypes.LEFT_PARENTHESIS, LensTokenTypes.RIGHT_PARENTHESIS, false)
        )
    }
}
