# LENS for JetBrains Rider

Editing support for `.lns` files in JetBrains Rider, with the same feature set as the VS Code
extension in `editors/vscode`.

Everything except the lexical colouring is answered by `lens-language-server` - the very same
language server the VS Code extension launches. No language logic is duplicated here: the plugin
registers a file type, ships a lexer for immediate colouring, and hands the file to the server over
the IntelliJ Platform LSP API.

The one exception is the debugger. Rider asks its ReSharper backend, not the frontend, what
breakpoints may go on a line, so the plugin has a second, .NET half - see
[The two halves](#the-two-halves) and [Breakpoints](#breakpoints).

## Features

| Feature | Where it comes from |
| --- | --- |
| Syntax highlighting | a lexer in the plugin, plus semantic tokens from the server |
| Diagnostics as you type | server |
| Completion | server |
| Hover | server |
| Go to definition | server |
| Find usages | server |
| Rename | server |
| File structure (outline) | server |
| Comment/uncomment, brace matching | plugin |
| Breakpoints in `.lns` files | the file type registration, plus the ReSharper backend half - see below |

## Prerequisites

* **JetBrains Rider 2026.1 or newer.** The plugin uses the IntelliJ Platform LSP API, which only
  exists in the paid IDEs, and a Rider extension point for the breakpoint gutter. It will not load
  in IntelliJ IDEA Community or Android Studio. 2026.1 is the first release in which every LSP
  feature the plugin relies on exists - see [Known gaps](#known-gaps-against-the-vs-code-extension).
* **A JDK 25 or newer**, to run Gradle. Rider 2026.x is compiled for Java 25, so an older JDK
  cannot read the platform classes; the JBR that ships inside Rider itself is a full JDK and works:
  `C:\Program Files\JetBrains\JetBrains Rider <version>\jbr`.
* **.NET SDK 10** on `PATH`, to build the language server that gets bundled into the plugin. Pass
  `-PbundleServer=false` to skip it. The same `dotnet` builds the ReSharper backend half, which
  targets `net8.0`; the targeting pack for it is restored from nuget.org on the first build. Pass
  `-PbundleBackend=false` to skip that.
* **A Rider containing `lib/DotNetSdkForRdPlugins`**, which is where the ReSharper SDK the backend
  compiles against lives. Every regular Rider installation has it; the build fails at configuration
  time, naming the path it expected, if the one given by `-PriderPath` does not.
* Gradle is **not** needed - use the wrapper (`./gradlew`, `gradlew.bat`).

## Building

The build has to compile against a Rider SDK. Pointing it at an installed Rider is much faster than
letting Gradle download one (the Rider SDK archive is around 4 GB):

```
cd editors/rider
gradlew.bat -PriderPath="C:/Program Files/JetBrains/JetBrains Rider 2026.2" buildPlugin
```

Without `-PriderPath`, the SDK named by `riderVersion` in `gradle.properties` is downloaded from the
JetBrains repository instead.

If the JDK on `PATH` is older than the one the target Rider needs, point Gradle at Rider's own:

```
set JAVA_HOME=C:\Program Files\JetBrains\JetBrains Rider 2026.2\jbr
```

The result is an installable zip:

```
editors/rider/build/distributions/lens-rider-<version>.zip
```

It contains the plugin jar, the backend assembly under `lens-rider/dotnet/` unless
`-PbundleBackend=false` was passed, and the published language server under `lens-rider/server/`
unless `-PbundleServer=false` was passed.

### Other useful tasks

| Task | What it does |
| --- | --- |
| `test` | runs the lexer and PSI tests |
| `verifyPluginProjectConfiguration` | checks the build settings against `since-build` |
| `verifyPlugin` | runs the JetBrains Plugin Verifier against the Rider given by `-PriderPath` |
| `runIde` | starts a sandbox Rider with the plugin installed |
| `publishLanguageServer` | runs `dotnet publish` for the server on its own |
| `prepare` | generates what `src/dotnet` needs to be built or opened on its own |
| `compileDotNet` | runs `dotnet build` for the backend half |

## The two halves

| Half | Where | What it does |
| --- | --- | --- |
| frontend | `src/main/kotlin` | file type, lexer, highlighter, LSP client, settings |
| backend | `src/dotnet/Lens.Rider.Backend` | teaches ReSharper that `.lns` is a source file, and answers breakpoint variants |

The backend is a normal ReSharper plugin assembly. It is built by `compileDotNet` and copied into
`lens-rider/dotnet/` of the plugin layout, which is where Rider's backend looks for the assemblies
of an installed plugin - nothing about it appears in `plugin.xml`.

It deliberately knows almost nothing about LENS. There is a language, a project file type for the
`.lns` extension, and a PSI so degenerate that the whole file is one token under one file node.
That is the least the breakpoint machinery accepts (see below), and `.lns` files are kept out of
ReSharper's caches and code model on top of it. Every real question about the language is still the
language server's.

`Lens.Rider.Backend.sln` can be opened on its own, but only after

```
gradlew.bat -PriderPath="C:/Program Files/JetBrains/JetBrains Rider 2026.2" prepare
```

which writes `build/DotNetSdkPath.Generated.props` and `src/dotnet/nuget.config`. Without them the
project fails to build with *Please run `./gradlew :prepare`* rather than a wall of missing
references.

## Installing

In Rider: **Settings | Plugins | gear icon | Install Plugin from Disk...**, pick the zip, restart.

The plugin declares `require-restart="true"`: the breakpoint extension point is not dynamic, and
the backend assembly is loaded by the ReSharper process on startup, so a restart is required rather
than optional.

## Finding the language server

The plugin looks for the server in this order:

1. the path in **Settings | Tools | LENS**;
2. the `LENS_LANGUAGE_SERVER` environment variable;
3. `server/lens-language-server.dll` (or `.exe`) inside the installed plugin.

A `.dll` is launched through `dotnet`, which can also be configured; a self-contained executable is
launched directly. If none of the three is found the plugin stays quiet and only the lexical
colouring works - it deliberately does not nag once per opened file.

## Breakpoints

Rider decides whether a line breakpoint may be placed like this
(`com.jetbrains.rider.debugger.breakpoint.DotNetLineBreakpointType.canPutAt`, decompiled from
Rider 2026.2):

1. `PsiManager.findFile(file)` - no PSI file, no breakpoint;
2. `RiderBreakpointHost.isSupportedLanguage(psiFile, line)`, which takes `psiFile.getLanguage()` and
   asks the `com.intellij.rider.debuggerSupportPolicy` language extension whether any registered
   policy allows that line.

A `.lns` file used to resolve to plain text, for which no policy is registered, so the gutter offered
nothing. This plugin supplies all three pieces the check needs:

* `<fileType>` maps `.lns` to the LENS language;
* `<lang.parserDefinition>` - **required**, because without it the platform builds a
  `PsiPlainTextFileImpl` whose language is `PlainText` and step 2 would still miss;
* `<rider.debuggerSupportPolicy language="LENS">`, registered with Rider's own permissive
  `RiderDebuggerSupportPolicy`, which allows every line.

This is exactly what the bundled F# plugin does for `.fs`.

Then, `DotNetLineBreakpointType.computeVariants` then asks the backend what
breakpoints may go on the line, and `isCreationOfDefaultBreakpointVariantAllowed()` is `false` - no
variants, no breakpoint. That request travels a long way:

1. `RiderBreakpointHost.requestVariantsComputation` calls `getPossibleBreakpointVariants` over the
   protocol;
2. the backend's `DebuggerBreakpointVariantsHost` waits for the solution to load, commits the PSI,
   and hands over to `BreakpointVariantsEnumerator`;
3. the enumerator resolves the path with `FindProjectItemsByLocation` and needs an `IProjectFile`;
4. it calls `GetPrimaryPsiFile()` on it and gives up if that is `null`;
5. it looks a provider up by `psiFile.Language`.

That is the whole reason the backend half exists, and why it has to go as far as building PSI:
`LensProjectFileType` gets step 3 an `IProjectFile`, `LensLanguageService` gets step 4 a one-token
tree, and `LensBreakpointVariantsProvider`, registered with `[Language(typeof(LensLanguage))]`, is
what step 5 finds. It returns a single `LineBreakpoint`, which becomes a line-wide variant with no
highlighted range - the same answer Rider gives for `.aspx`. Deciding that a line is blank, a
comment or a statement is left to `canPutAt` and to the language server.

Two consequences fall out of steps 2 and 3, and neither is something this plugin can change:

* **The session needs a solution.** `RiderBreakpointHost` has no breakpoint helper without one, and
  `DebuggerBreakpointVariantsHost` waits for startup to finish. In a folder-mode Rider the gutter
  offers nothing.
* **The file needs to be in the project model.** A `.lns` file that belongs to no project reaches
  step 3 and stops there.

Note that a script can always stop the debugger itself with `Debugger::Break ()`; this is only about
the gutter.

## Known gaps against the VS Code extension

* **Indentation rules.** `language-configuration.json` teaches VS Code to indent after `then`, `->`,
  `record` and friends. The IntelliJ equivalent is a formatter model, which is not implemented -
  Enter keeps the previous indent.
* **Interpolation holes are not coloured as code.** The lexer treats an interpolated string as one
  token; the server's semantic tokens still colour the names inside it.
* The plugin still uses the pre-2026.1 LSP API names (`LspServerSupportProvider`,
  `ProjectWideLspServerDescriptor`), which are deprecated in the current platform. They were the
  only ones available while Rider 2025.2 was supported; now that `since-build` is 261 they can be
  replaced with the current API, and that has not been done yet.
* **The backend half has no automated tests.** `test` covers the lexer and the PSI on the JVM only;
  the backend is exercised by hand in `runIde`. A ReSharper test harness would be a bigger addition
  than the six small components it would be testing.
