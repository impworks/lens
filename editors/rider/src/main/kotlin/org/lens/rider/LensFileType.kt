package org.lens.rider

import com.intellij.openapi.fileTypes.LanguageFileType
import com.intellij.openapi.util.IconLoader
import javax.swing.Icon

/**
 * Binds the ".lns" extension to the LENS language.
 */
object LensFileType : LanguageFileType(LensLanguage) {

    const val EXTENSION = "lns"

    private val fileIcon: Icon by lazy { IconLoader.getIcon("/icons/lens.svg", LensFileType::class.java) }

    override fun getName() = "LENS"

    override fun getDescription() = "LENS script"

    override fun getDefaultExtension() = EXTENSION

    override fun getIcon() = fileIcon
}
