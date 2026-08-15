# LENS for VS Code

Language support for [LENS](https://github.com/impworks/lens), the embeddable .NET scripting
language: highlighting, completion, diagnostics, navigation and rename for `.lns` files.

## What it does

| Feature | Notes |
|---|---|
| Syntax highlighting | A TextMate grammar for the shape of the file, and **semantic colouring from the compiler** on top of it — so a bare name is coloured as the record, argument or function it actually is. |
| Diagnostics | Everything the compiler finds, as you type. Several independent errors are reported as several problems, not just the first. |
| Completion | Instance members after `.` (extension methods and LINQ included), static members after `::`, and every name in scope elsewhere. The two are kept apart, so `::` never offers what would not compile. |
| Hover | The type or signature of whatever is under the pointer. |
| Go to definition | Locals, arguments, functions, records, algebraic types and record fields. |
| Find references | The same set. |
| Rename | The same set. Anything the script does not own — a .NET member, a host type, a standard library function — is refused rather than half-done. |
| Outline | Records, algebraic types, functions and `declare` blocks, in the outline view and the breadcrumb bar. |

A file that is halfway through being typed still works: a statement that does not parse is reported
and skipped, and everything around it keeps its colouring, its completions and its outline.

## Telling the editor what the host provides

A LENS script is glue code — it exists to call an API the *host application* registers. An editor
has no host, so it has no way to know what `screen` or `clamp` are.

Declare them at the top of the file and it does:

```lens
declare
    reference "FooBar.dll"
    let screen : ScreenManager
    var counter : int
    fun clamp:int (value:int low:int high:int)
    type Baz = MyNamespace.Foo.Baz.SomeType

screen.Clear ()
counter = clamp counter 0 100
```

The same block is checked by the compiler when the script runs for real, against what the host
actually registered — so the editor's view cannot drift from the truth without the build saying so.

`reference` lines are resolved relative to the script. One that points at a file which is not there
is reported as a warning, never an error: the host chooses its own assemblies, so an unresolvable
path says nothing about whether the script will run.

## Installing

The extension ships with the language server, which needs the [.NET 8
runtime](https://dotnet.microsoft.com/download) on the machine.

To build both from a clone of the repository:

```
cd editors/vscode
npm install
npm run build-server     # publishes the server into ./server
npm run compile          # prints nothing when it succeeds
```

### Trying it without installing

Open **this folder** in VS Code — not the repository root, since the launch configuration lives in
`editors/vscode/.vscode/` and VS Code only reads the one belonging to the folder you opened:

```
code D:/path/to/lens/editors/vscode
```

Press <kbd>F5</kbd>. A second window titled *Extension Development Host* opens with `samples/` as its
workspace; open `demo.lns` in it and the extension is running.

### Installing it properly

```
npm run package
code --install-extension lens-lang-5.0.0.vsix
```

Then reload VS Code and open any `.lns` file, from any folder.

### Rebuilding the server while it is running

`npm run build-server` copies over `server/lens-language-server.dll`, which a running server holds
open. Close the Extension Development Host window (or reload it, or the window it was launched from)
before rebuilding, or the publish fails with "the file is locked" and the extension keeps running
the old build.

### Checking the server on its own

If something is not answering, the server can be exercised without an editor at all:

```
npm run smoke-test
```

That drives a real server process over the protocol and checks each feature in turn.

## Settings

| Setting | Default | What it does |
|---|---|---|
| `lens.server.path` | *(empty)* | Path to `lens-language-server.dll`, or to a self-contained executable. Empty means the copy bundled with the extension. |
| `lens.server.dotnetPath` | `dotnet` | The dotnet host used to run the server when it is a `.dll`. |
| `lens.trace.server` | `off` | Logs the traffic between VS Code and the server, for when something is not answering. |

## Using LENS from another editor

The server speaks the language server protocol over stdio and has nothing VS Code-specific in it:

```
dotnet path/to/lens-language-server.dll
```

Any client that can launch that — Neovim, Zed, Sublime, Visual Studio, Rider — gets the same
features. An editor plugin that would rather host the language services in-process can reference
`Lens.LanguageServer.Core` instead and skip the protocol entirely.
