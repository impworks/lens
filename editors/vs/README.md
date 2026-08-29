# LENS for Visual Studio

Language support for [LENS](https://github.com/impworks/lens), the embeddable .NET scripting
language: highlighting, completion, diagnostics, navigation and rename for `.lns` files in Visual
Studio 2022 and Visual Studio 2026.

The extension is a thin shell. Every answer it gives comes from `lens-language-server`, the same
language server the VS Code extension talks to, launched as a child process and spoken to over
stdio. There is no second implementation of anything to drift out of step.

## What it does

| Feature | Notes |
|---|---|
| Syntax highlighting | The TextMate grammar from the VS Code extension, registered as a grammar repository. |
| Diagnostics | Everything the compiler finds, as you type, in the error list and squiggled in the editor. |
| Completion | Instance members after `.`, static members after `::`, type names after `new`, namespaces in a `use` directive, and every name in scope elsewhere. |
| Quick info | The type or signature of whatever is under the pointer. |
| Go to definition | Locals, arguments, functions, records, algebraic types and record fields. |
| Find all references | The same set. |
| Rename | The same set. Anything the script does not own is refused rather than half-done. |
| Outline | Records, algebraic types, functions and `declare` blocks. |
| Debugger data tips | Hovering a variable while stopped at a breakpoint - see the caveats below. |

Brackets, comment characters and auto-closing pairs come from the same
`language-configuration.json` the VS Code extension uses, mapped onto the content type in
`LensGrammars.pkgdef`.

For what a `declare` block is and why an editor needs one, see
[`editors/vscode/README.md`](../vscode/README.md) - it is the same server, so the same rules apply.

## Prerequisites

To **use** the extension:

- Visual Studio 2022 (17.0) or newer, x64. The manifest targets `[17.0,19.0)`, which covers VS 2026.
- The [.NET 10 runtime](https://dotnet.microsoft.com/download), which is what the bundled language
  server runs on.

To **build** it:

- The same Visual Studio, for MSBuild. The **Visual Studio extension development** workload is *not*
  required - the VSIX build targets come from the `Microsoft.VSSDK.BuildTools` NuGet package, which
  the project restores like any other dependency.
- The .NET 10 SDK, to publish the language server.

## Building

```powershell
cd editors\vs
.\build.ps1
```

That publishes the language server into `Lens.VisualStudio\server` and then builds the VSIX around
it, ending at:

```
editors\vs\Lens.VisualStudio\bin\Release\net472\Lens.VisualStudio.vsix
```

`build.ps1 -SkipServer` leaves `server\` alone, which is what you want while iterating on the
extension itself: a Visual Studio instance that has the extension loaded holds the server files
open, and the publish then fails with a locked file.

The project is deliberately **not** part of `Lens.sln`. It is a .NET Framework project with VSIX
targets, and adding it would break `dotnet build Lens.sln`, which is what CI runs. Open
`editors\vs\Lens.VisualStudio.sln` instead.

## Installing

Close every Visual Studio instance first - the installer cannot replace files that are in use - then
either double-click the `.vsix`, or:

```powershell
& "$env:ProgramFiles\Microsoft Visual Studio\18\Community\Common7\IDE\VSIXInstaller.exe" `
    Lens.VisualStudio\bin\Release\net472\Lens.VisualStudio.vsix
```

Then open a folder containing `.lns` files (**File > Open > Folder**, not a solution). The extension
loads the first time a `.lns` file is opened, and Visual Studio's language server support is built
around the open-folder workspace - a loose file opened outside any folder gets highlighting but a
reduced set of the rest.

To uninstall: **Extensions > Manage Extensions**, find *LENS*, uninstall, restart.

### Running it without installing

```powershell
.\build.ps1 -Configuration Debug
& "$env:ProgramFiles\Microsoft Visual Studio\18\Community\Common7\IDE\VSIXInstaller.exe" `
    /rootSuffix:Exp Lens.VisualStudio\bin\Debug\net472\Lens.VisualStudio.vsix
devenv.exe /rootSuffix Exp
```

The experimental instance is a separate hive, so nothing here disturbs the Visual Studio you work
in. To debug the extension itself, attach to that `devenv.exe`.

## Pointing at a different language server

The extension normally runs the copy of the server bundled inside it. Two environment variables
override that, read by the Visual Studio process at startup:

| Variable | What it does |
|---|---|
| `LENS_LANGUAGE_SERVER` | Path to a `lens-language-server.dll`, or to a self-contained executable, to run instead of the bundled copy. |
| `LENS_LANGUAGE_SERVER_DOTNET` | The dotnet host used when the path above is a `.dll`. Defaults to `dotnet` from the `PATH`. |

These are environment variables rather than Visual Studio settings because the decision of what to
launch has to be made before the server exists, and Visual Studio settings only reach the extension
over the protocol, after it is already running.

`LensSettings.json` carries what does travel over the protocol. In an open folder, a user overrides
it in `.vs\VSWorkspaceSettings.json`:

```json
{
    "lens.trace.server": "Verbose"
}
```

With tracing on, the traffic is written to `%TEMP%\VisualStudio\LSP\LENS Language Server-*.log`.

## Debugger data tips

LENS scripts compiled with debug information can be debugged inside a host application: breakpoints,
stepping and the Locals window work off the debug engine and the emitted PDB, and never involve the
editor at all. Hovering a variable in break mode is different - it is the *editor* that starts it,
and it goes like this:

1. The editor asks the command filter chain of the text view for
   `IVsTextViewFilter.GetDataTipText`, handing it the word it guessed at.
2. The filter widens that to the whole expression and passes it to
   `IVsDebugger.GetDataTipValue`, which evaluates it in the current stack frame.
3. If no filter in the chain answers, no tip appears. There is no fallback.

Registering a content type, a file extension and a language client does **not** put a filter on the
view, which is why data tips did nothing before this extension existed. `LensDataTipFilter.cs` adds
one. Note that this is not part of the language server protocol: Visual Studio's LSP client has no
debugger integration and never turns `textDocument/hover` into a data tip.

Two things to know about what this does and does not buy:

- **The expression is scanned out of the buffer, not asked of the server.** The call happens on the
  UI thread with the debugger waiting on it, so a cross-process round trip is not an option. A name
  and the member chain leading up to it are recognised - `count`, `player.Position.X`. An index or a
  call is not; those go in the watch window.
- **Whatever comes back is evaluated by the C# expression evaluator.** Visual Studio picks an
  expression evaluator from the language recorded in the PDB and falls back to the C# one when the
  language is unknown, which is what makes the Locals window work today. So a plain name or a dotted
  member chain evaluates; anything with LENS-specific syntax will not. Changing that means writing a
  Concord expression evaluator - `IDkmClrExpressionCompiler`, `IDkmClrFormatter`,
  `IDkmLanguageFrameDecoder`, a `.vsdconfigxml` and a `DebuggerEngineExtension` asset - which is a
  separate project, and unnecessary for the ordinary "what is this variable" case.

If a tip shows an evaluator error such as *identifier not found*, the filter is working and the
problem is the expression. If nothing appears at all, the filter is not being reached.

## How it fits together

| File | What it is |
|---|---|
| `LensContentDefinition.cs` | Declares the `lens` content type and binds `.lns` to it. Nothing in the extension loads until a file of this type is opened. |
| `LensLanguageClient.cs` | The `ILanguageClient` Visual Studio drives. Starts the server and hands over its streams. |
| `LensServerLocator.cs` | Decides which server to start and how. |
| `LensDataTipFilter.cs` | The `IVsTextViewFilter` behind debugger data tips. |
| `Grammars/` | The TextMate grammar and language configuration, copied from the VS Code extension. The grammar copy declares `fileTypes` because Visual Studio matches grammars by what the grammar itself claims, where VS Code takes that from `package.json`. |
| `LensGrammars.pkgdef` | Registers the grammar folder, the language configuration and the settings defaults. |
| `LensSettings.json` | Defaults for the settings the server reads. |

## Using LENS from another editor

The server has nothing editor-specific in it and speaks the protocol over stdio:

```
dotnet path/to/lens-language-server.dll
```

An editor plugin that would rather host the language services in-process can reference
`Lens.LanguageServer.Core` and skip the protocol entirely.
