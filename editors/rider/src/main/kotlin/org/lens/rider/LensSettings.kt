package org.lens.rider

import com.intellij.openapi.application.ApplicationManager
import com.intellij.openapi.components.PersistentStateComponent
import com.intellij.openapi.components.State
import com.intellij.openapi.components.Storage
import com.intellij.openapi.fileChooser.FileChooser
import com.intellij.openapi.fileChooser.FileChooserDescriptorFactory
import com.intellij.openapi.options.Configurable
import com.intellij.openapi.ui.TextFieldWithBrowseButton
import com.intellij.ui.components.JBLabel
import com.intellij.ui.components.JBTextField
import com.intellij.util.ui.FormBuilder
import javax.swing.JComponent
import javax.swing.JPanel

/**
 * Where to find the language server, mirroring the two settings the VS Code extension offers.
 */
@State(name = "LensSettings", storages = [Storage("lens.xml")])
class LensSettings : PersistentStateComponent<LensSettings.State> {

    class State {
        /**
         * A lens-language-server.dll or a self-contained executable. Empty means the copy bundled
         * with the plugin.
         */
        @JvmField
        var serverPath: String = ""

        /**
         * The dotnet host used when the server is a .dll.
         */
        @JvmField
        var dotnetPath: String = "dotnet"
    }

    private var state = State()

    override fun getState() = state

    override fun loadState(state: State) {
        this.state = state
    }

    var serverPath: String
        get() = state.serverPath
        set(value) {
            state.serverPath = value
        }

    var dotnetPath: String
        get() = state.dotnetPath.ifBlank { "dotnet" }
        set(value) {
            state.dotnetPath = value
        }

    companion object {
        fun getInstance(): LensSettings = ApplicationManager.getApplication().getService(LensSettings::class.java)
    }
}

class LensConfigurable : Configurable {

    private lateinit var serverPath: TextFieldWithBrowseButton
    private lateinit var dotnetPath: JBTextField

    override fun getDisplayName() = "LENS"

    override fun createComponent(): JComponent {
        serverPath = TextFieldWithBrowseButton()
        dotnetPath = JBTextField()

        // the descriptor is built by hand because the browse-folder helpers have changed signature
        // between the platform versions this plugin supports
        serverPath.addActionListener {
            val descriptor = FileChooserDescriptorFactory.createSingleFileDescriptor()
            val chosen = FileChooser.chooseFile(descriptor, null, null)

            if (chosen != null)
                serverPath.text = chosen.presentableUrl
        }

        val hint = JBLabel(
            "Leave empty to use the server bundled with the plugin, or the one named by the " +
            "LENS_LANGUAGE_SERVER environment variable."
        )

        return FormBuilder.createFormBuilder()
            .addLabeledComponent("Language server path:", serverPath, 1, false)
            .addComponent(hint)
            .addLabeledComponent("dotnet executable:", dotnetPath, 1, false)
            .addComponentFillVertically(JPanel(), 0)
            .panel
    }

    override fun isModified(): Boolean {
        val settings = LensSettings.getInstance()
        return serverPath.text != settings.serverPath || dotnetPath.text != settings.dotnetPath
    }

    override fun apply() {
        val settings = LensSettings.getInstance()
        settings.serverPath = serverPath.text.trim()
        settings.dotnetPath = dotnetPath.text.trim()
    }

    override fun reset() {
        val settings = LensSettings.getInstance()
        serverPath.text = settings.serverPath
        dotnetPath.text = settings.dotnetPath
    }
}
