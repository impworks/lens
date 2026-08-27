# LENS for JetBrains Rider

Editing support for `.lns` files in JetBrains Rider, with the same feature set as the VS Code
extension in `editors/vscode`.

Everything except the lexical colouring is answered by `lens-language-server` - the very same
language server the VS Code extension launches. No language logic is duplicated here: the plugin
registers a file type, ships a lexer for immediate colouring, and hands the file to the server over
the IntelliJ Platform LSP API.

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
| Breakpoints in `.lns` files | the file type registration - see below |

## Prerequisites

* **JetBrains Rider 2025.2 or newer.** The plugin uses the IntelliJ Platform LSP API, which only
  exists in the paid IDEs, and a Rider extension point for the breakpoint gutter. It will not load
  in IntelliJ IDEA Community or Android Studio.
* **A JDK matching the Rider you build against**, to run Gradle. Rider 2026.x is compiled for Java
  25; the JBR that ships inside Rider itself is a full JDK and works:
  `C:\Program Files\JetBrains\JetBrains Rider <version>\jbr`.
* **.NET SDK 10** on `PATH`, to build the language server that gets bundled into the plugin. Pass
  `-PbundleServer=false` to skip it.
* Gradle is **not** needed - use the wrapper (`./gradlew`, `gradlew.bat`).

## Building

The build has to compile against a Rider SDK. Pointing it at an installed Rider is much faster than
letting Gradle download one (the Rider SDK archive is around 4 GB):

```
cd editors/rider
gradlew.bat -PriderPath="C:/Program Files/JetBrains/JetBrains Rider 2025.2" buildPlugin
```

Without `-PriderPath`, the SDK named by `riderVersion` in `gradle.properties` is downloaded from the
JetBrains repository instead.

If the JDK on `PATH` is older than the one the target Rider needs, point Gradle at Rider's own:

```
set JAVA_HOME=C:\Program Files\JetBrains\JetBrains Rider 2025.2\jbr
```

The result is an installable zip:

```
editors/rider/build/distributions/lens-rider-<version>.zip
```

It contains the plugin jar and, unless `-PbundleServer=false` was passed, the published language
server under `lens-rider/server/`.

### Other useful tasks

| Task | What it does |
| --- | --- |
| `test` | runs the lexer and PSI tests |
| `verifyPluginProjectConfiguration` | checks the build settings against `since-build` |
| `verifyPlugin` | runs the JetBrains Plugin Verifier against the Rider given by `-PriderPath` |
| `runIde` | starts a sandbox Rider with the plugin installed |
| `publishLanguageServer` | runs `dotnet publish` for the server on its own |

## Installing

In Rider: **Settings | Plugins | gear icon | Install Plugin from Disk...**, pick the zip, restart.

The breakpoint extension point is not dynamic, so a restart is required rather than optional.

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

What has **not** been observed working, and needs a debugging session to confirm: whether a
breakpoint set this way actually binds and is hit. The payload Rider sends to the debugger worker is
a document path plus offsets, and the worker resolves it against the sequence points in the PDB, so
a script compiled with debug information should bind on file path alone. The risk is elsewhere:
`DotNetLineBreakpointType.computeVariants` asks the ReSharper backend for the breakpoint variants on
a line, and there is no backend language for `.lns`. If the gutter turns out to offer a breakpoint
that never binds, the fix is the route Rider's own Unity plugin takes - subclass
`DotNetLineBreakpointType` through its protected `(id, title)` constructor, return a single line-wide
variant, and register a matching handler through `com.intellij.rider.debug.breakpoint.handler.factory`.

Note that a script can always stop the debugger itself with `Debugger::Break ()`; this is only about
the gutter.

## Known gaps against the VS Code extension

* **Indentation rules.** `language-configuration.json` teaches VS Code to indent after `then`, `->`,
  `record` and friends. The IntelliJ equivalent is a formatter model, which is not implemented -
  Enter keeps the previous indent.
* **Interpolation holes are not coloured as code.** The lexer treats an interpolated string as one
  token; the server's semantic tokens still colour the names inside it.
* **Rider version differences.** The platform gained LSP features over time: document symbols (the
  file structure) arrived in 2025.3 and rename in 2026.1. On Rider 2025.2 those two are unavailable
  however the plugin is written; everything else works from 2025.2 onwards.
* The plugin uses the pre-2026.1 LSP API names (`LspServerSupportProvider`,
  `ProjectWideLspServerDescriptor`) on purpose. They are deprecated in the newest platform but are
  the only ones that exist on the older Riders this plugin still supports.
