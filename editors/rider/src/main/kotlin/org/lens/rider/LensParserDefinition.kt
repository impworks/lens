package org.lens.rider

import com.intellij.extapi.psi.ASTWrapperPsiElement
import com.intellij.extapi.psi.PsiFileBase
import com.intellij.lang.ASTNode
import com.intellij.lang.ParserDefinition
import com.intellij.lang.PsiBuilder
import com.intellij.lang.PsiParser
import com.intellij.openapi.project.Project
import com.intellij.psi.FileViewProvider
import com.intellij.psi.PsiElement
import com.intellij.psi.tree.IElementType
import com.intellij.psi.tree.IFileElementType

/**
 * The PSI file for a LENS script.
 *
 * The tree is flat - one node per lexer token. Nothing in the plugin walks it, but it has to exist:
 * without a parser definition the platform hands out a plain text PSI file, whose language is not
 * LENS, and the two features that are keyed on the language - the breakpoint gutter and the
 * language-scoped extensions below - would both miss it.
 */
class LensFile(viewProvider: FileViewProvider) : PsiFileBase(viewProvider, LensLanguage) {

    override fun getFileType() = LensFileType

    override fun toString() = "LENS script"
}

class LensParserDefinition : ParserDefinition {

    override fun createLexer(project: Project?) = LensLexer()

    override fun createParser(project: Project?) = LensParser()

    override fun getFileNodeType() = FILE

    override fun getCommentTokens() = LensTokenTypes.COMMENTS

    override fun getStringLiteralElements() = LensTokenTypes.STRINGS

    override fun createElement(node: ASTNode): PsiElement = ASTWrapperPsiElement(node)

    override fun createFile(viewProvider: FileViewProvider) = LensFile(viewProvider)

    companion object {
        val FILE = IFileElementType(LensLanguage)
    }
}

class LensParser : PsiParser {

    override fun parse(root: IElementType, builder: PsiBuilder): ASTNode {
        val file = builder.mark()

        while (!builder.eof())
            builder.advanceLexer()

        file.done(root)
        return builder.treeBuilt
    }
}
