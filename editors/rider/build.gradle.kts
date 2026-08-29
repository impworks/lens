import org.jetbrains.intellij.platform.gradle.IntelliJPlatformType
import org.jetbrains.kotlin.gradle.dsl.JvmTarget
import org.jetbrains.intellij.platform.gradle.TestFrameworkType
import org.jetbrains.intellij.platform.gradle.tasks.PrepareSandboxTask
import kotlin.io.path.absolute
import kotlin.io.path.isDirectory

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

val buildConfiguration = providers.gradleProperty("buildConfiguration").getOrElse("Release")
val dotNetPluginId = providers.gradleProperty("dotNetPluginId").getOrElse("Lens.Rider.Backend")
val dotNetSrcDir = projectDir.resolve("src/dotnet")
val dotNetSdkPropsFile = layout.buildDirectory.file("DotNetSdkPath.Generated.props")

// The ReSharper SDK the backend compiles against lives inside the Rider being built against; there
// is no NuGet feed for it. Resolved lazily, because touching platformPath during configuration
// would force the SDK to be resolved even for tasks that do not need it.
val riderSdkPath by lazy {
    val path = intellijPlatform.platformPath.resolve("lib/DotNetSdkForRdPlugins").absolute()
    require(path.isDirectory()) { "$path does not exist or is not a directory" }
    path
}

// MSBuild decides whether to rebuild by timestamp, so rewriting an unchanged file is not free
fun File.writeTextIfChanged(text: String) {
    val bytes = text.toByteArray()

    if (!exists() || !readBytes().contentEquals(bytes)) {
        parentFile.mkdirs()
        writeBytes(bytes)
    }
}

val generateDotNetSdkProperties = tasks.register("generateDotNetSdkProperties") {
    description = "Writes the props file that tells the backend project where the ReSharper SDK is."
    group = "build setup"

    val target = dotNetSdkPropsFile

    doLast {
        target.get().asFile.writeTextIfChanged(
            """
            <Project>
              <PropertyGroup>
                <DotNetSdkPath>$riderSdkPath</DotNetSdkPath>
              </PropertyGroup>
            </Project>
            """.trimIndent() + "\n"
        )
    }
}

val generateNuGetConfig = tasks.register("generateNuGetConfig") {
    description = "Writes the NuGet configuration the backend project restores through."
    group = "build setup"

    val target = dotNetSrcDir.resolve("nuget.config")

    doLast {
        target.writeTextIfChanged(
            """
            <?xml version="1.0" encoding="utf-8"?>
            <!-- generated by the "generateNuGetConfig" Gradle task - run `gradlew :prepare` to refresh -->
            <configuration>
                <packageSources>
                    <add key="rider-sdk" value="$riderSdkPath" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                </packageSources>
            </configuration>
            """.trimIndent() + "\n"
        )
    }
}

val prepare = tasks.register("prepare") {
    description = "Generates everything src/dotnet needs to be built, or opened in an IDE, on its own."
    group = "build setup"

    dependsOn(generateDotNetSdkProperties, generateNuGetConfig)
}

val compileDotNet = tasks.register<Exec>("compileDotNet") {
    description = "Builds the ReSharper backend assembly that is bundled with the plugin."
    group = "build"

    dependsOn(prepare)

    // bin and obj change on every build, so only the sources and the SDK path decide
    inputs.files(
        fileTree(dotNetSrcDir) {
            include("**/*.cs", "**/*.csproj", "**/*.props", "**/*.sln")
            exclude("**/bin/**", "**/obj/**")
        }
    ).withPathSensitivity(PathSensitivity.RELATIVE)
    inputs.file(dotNetSdkPropsFile).withPathSensitivity(PathSensitivity.NONE)
    inputs.property("buildConfiguration", buildConfiguration)

    outputs.dir(dotNetSrcDir.resolve("$dotNetPluginId/bin"))

    workingDir = dotNetSrcDir
    executable(providers.gradleProperty("dotnetPath").getOrElse("dotnet"))
    args("build", "--configuration", buildConfiguration, "-consoleLoggerParameters:ErrorsOnly")
}

tasks.withType<PrepareSandboxTask>().configureEach {
    if (bundleServer) {
        from(publishLanguageServer) {
            into("${pluginName.get()}/server")
        }
    }

    // Rider's backend picks plugin assemblies up from the "dotnet" folder of an installed
    // plugin, which is why nothing has to be declared in plugin.xml for this.
    val backendOutput = dotNetSrcDir.resolve("$dotNetPluginId/bin/$dotNetPluginId/$buildConfiguration")
    val backendFiles = listOf(
        backendOutput.resolve("$dotNetPluginId.dll"),
        backendOutput.resolve("$dotNetPluginId.pdb")
    )

    dependsOn(compileDotNet)

    from(backendFiles) {
        into("${pluginName.get()}/dotnet")
    }

    // a Copy silently ignores a source that is not there, and a plugin without its backend
    // assembly would only fail much later, inside Rider
    doLast {
        backendFiles.forEach { require(it.exists()) { "\"$it\" does not exist." } }
    }
}

tasks.buildPlugin {
    dependsOn(compileDotNet)
}
