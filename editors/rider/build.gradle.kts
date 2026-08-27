import org.jetbrains.intellij.platform.gradle.IntelliJPlatformType
import org.jetbrains.kotlin.gradle.dsl.JvmTarget
import org.jetbrains.intellij.platform.gradle.TestFrameworkType
import org.jetbrains.intellij.platform.gradle.tasks.PrepareSandboxTask

plugins {
    kotlin("jvm") version "2.4.10"
    id("org.jetbrains.intellij.platform") version "2.18.1"
}

group = "org.lens"
version = providers.gradleProperty("pluginVersion").getOrElse("5.0.0")

repositories {
    mavenCentral()

    intellijPlatform {
        defaultRepositories()
    }
}

dependencies {
    intellijPlatform {
        // Rider is the only IDE this plugin targets: the LSP API is paid-IDE only and the
        // breakpoint gutter hook lives in the Rider plugin. A local installation is used when one
        // is pointed at, because the downloadable SDK is well over a gigabyte.
        val local = providers.gradleProperty("riderPath").orNull

        if (local.isNullOrBlank())
            create(IntelliJPlatformType.Rider, providers.gradleProperty("riderVersion").get()) {
                // Rider has no installer artifact the plugin can consume
                useInstaller = false
            }
        else
            local(local)

        testFramework(TestFrameworkType.Platform)
    }

    testImplementation("junit:junit:4.13.2")
    testImplementation("org.opentest4j:opentest4j:1.3.0")
}

tasks.test {
    useJUnit()
}

// The newest Rider runs on Java 25, but a plugin compiled for it cannot be loaded by the 2025.x
// releases this one still supports, so the bytecode is held at the level "sinceBuild" implies. The
// IntelliJ Platform plugin raises both targets to whatever the platform being compiled against
// uses, and does so late, which is why the values are put back afterwards.
afterEvaluate {
    tasks.withType<org.jetbrains.kotlin.gradle.tasks.KotlinCompile>().configureEach {
        compilerOptions.jvmTarget.set(JvmTarget.JVM_21)
    }

    java {
        sourceCompatibility = JavaVersion.VERSION_21
        targetCompatibility = JavaVersion.VERSION_21
    }

    tasks.withType<JavaCompile>().configureEach {
        sourceCompatibility = JavaVersion.VERSION_21.toString()
        targetCompatibility = JavaVersion.VERSION_21.toString()
    }
}

intellijPlatform {
    pluginConfiguration {
        version = project.version.toString()

        ideaVersion {
            sinceBuild = providers.gradleProperty("sinceBuild")
            untilBuild = provider { null }
        }
    }

    buildSearchableOptions = false

    pluginVerification {
        ides {
            providers.gradleProperty("riderPath").orNull?.takeIf { it.isNotBlank() }?.let { local(it) }
        }
    }
}

// The language server is a .NET executable that has to exist next to the plugin, mirroring what
// the VS Code extension does in its "build-server" script. It is opt-out because a plugin without
// a server can only highlight.
val bundleServer = providers.gradleProperty("bundleServer").getOrElse("true").toBoolean()
val serverOutput = layout.buildDirectory.dir("languageServer")

val publishLanguageServer = tasks.register<Exec>("publishLanguageServer") {
    description = "Publishes the LENS language server into the directory that is bundled with the plugin."
    group = "build"

    val repository = rootProject.file("../..")
    val serverProject = repository.resolve("Lens.LanguageServer/Lens.LanguageServer.csproj")

    // only the sources decide whether a republish is needed - bin and obj change on every build
    inputs.files(
        fileTree(repository) {
            include("Lens/**/*.cs", "Lens/*.csproj")
            include("Lens.LanguageServer/**/*.cs", "Lens.LanguageServer/*.csproj")
            include("Lens.LanguageServer.Core/**/*.cs", "Lens.LanguageServer.Core/*.csproj")
            exclude("**/bin/**", "**/obj/**")
        }
    ).withPathSensitivity(PathSensitivity.RELATIVE)

    outputs.dir(serverOutput)

    commandLine(
        providers.gradleProperty("dotnetPath").getOrElse("dotnet"),
        "publish",
        serverProject.absolutePath,
        "-c", "Release",
        "-o", serverOutput.get().asFile.absolutePath
    )
}

tasks.withType<PrepareSandboxTask>().configureEach {
    if (bundleServer) {
        from(publishLanguageServer) {
            into("${pluginName.get()}/server")
        }
    }
}
