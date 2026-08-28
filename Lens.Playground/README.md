# LENS Playground

A web page where somebody who has never heard of LENS can type a script and press F8.

The whole compiler runs in the browser. There is no backend, no API, and nothing is uploaded:
the page ships the .NET runtime, the LENS compiler and the LENS language services as WebAssembly,
and everything after the download happens on the visitor's own machine. That is also why it can
be hosted as a folder of static files behind any web server.

## What is in it

| Piece | Where it comes from |
| --- | --- |
| Editor | Monaco, restored from npm and served from the image (only the core editor - see below) |
| Highlighting, completion, hover, go to definition, find references, rename, outline | `Lens.LanguageServer.Core`, the same code the LSP server and the editor plugins use |
| Compiling and running | `Lens`, unchanged |
| Serving | Caddy, static files only |

The editor asks its questions through `Interop`, a small set of `[JSInvokable]` methods. Monaco
counts lines from one and the language service counts from zero; the conversion lives in
`toService`/`fromService` in `wwwroot/js/playground.js` and nowhere else.

## What is taken from Monaco

The npm package is 14 MB, and the build copies 4.2 MB of it into `wwwroot/lib/monaco`: the loader,
`editor/`, and `base/`. Left behind are the grammars for the eighty-odd languages Monaco ships with,
the JSON, CSS, HTML and TypeScript language services, and the translated messages. The editor loads
all of those lazily - per language, per locale - and this page registers its own language and is in
English, so none of them was ever requested.

`CollectMonaco` in the project file both copies and prunes, so narrowing that list again does not
leave the old files behind in a working tree that has already been built.

## Running it

```bash
# from the repository root
dotnet run --project Lens.Playground
```

The first build restores Monaco with `npm ci`, so Node has to be on the path. After that:

```bash
docker build -f Lens.Playground/Dockerfile -t lens-playground .
docker run --rm -p 8080:8080 lens-playground
```

The container is a Caddy image with the published site in `/srv`. It listens on `$PORT`, which
Railway sets, and falls back to 8080.

### Deploying to Railway

The service is configured in Railway's own settings rather than by a file in the repository, as
Config as Code is being retired. The settings that matter:

- **Root directory**: the repository root, not `Lens.Playground`. The Dockerfile needs the `Lens`
  and `Lens.LanguageServer.Core` projects in its build context, and a root directory of
  `Lens.Playground` puts them outside it.
- **Builder**: Dockerfile, with the path `Lens.Playground/Dockerfile`. The default is Railpack,
  which has nothing to go on here.
- **Health check**: `/index.html`, with a 30 second timeout.
- **Watch paths**, if used, have to cover everything the image is built from, not just this
  folder: `/Lens.Playground/**`, `/Lens/**`, `/Lens.LanguageServer.Core/**`. A change to the
  compiler changes the playground, because the playground compiles Lens in the browser.

## The environment a script gets

- **The .NET base class libraries**, including LINQ and `HttpClient`. `System.Text.Json` and
  `System.Net.Http` are referenced explicitly; the rest come with the compiler's defaults.
- **The network**, through the browser. Requests are subject to the browser's cross-origin rules,
  so the site being called has to allow them.
- **Console output**, exactly as in the console host: `print` writes, `println` writes a line.
- **Console input**, from the Input pane. Its contents become standard input, so `readln` reads
  the pane line by line and returns null past the end.

What a script does **not** get:

- **`declare reference`**. There is no file system to load an assembly from. A script that asks
  for one is refused by name, with the entry underlined.
- **The file system, processes, and the host environment.** `File`, `Directory`, `Process`,
  `Environment` and friends are rejected at compile time rather than left to fail at run time -
  see `PlaygroundOptions`. The browser would refuse them anyway; the blacklist only makes the
  refusal legible.
- **`System.Reflection.Emit` and `System.Runtime.Loader`.** Emitting IL is how the playground runs
  a script at all, so reaching it from inside a script would be a way around everything above.

## Known limits

**A script that never finishes freezes the tab.** The browser gives a page one thread, the script
runs on it, and LENS has no way to interrupt a running script. A `while true` loop with no `await`
in it holds that thread until the tab is closed. Everything else - a slow loop, a long chain of
requests - merely feels slow.

**Arrays of a type that is still being compiled do not work.** That means arrays of records and
algebraic types declared in the script, and arrays of a generic parameter. This is a limitation of
the .NET runtime that browsers run, not of LENS: declaring a local needs a real CLR type, and the
array of a type that has not been created yet is a synthetic type object that Mono's
`ILGenerator.DeclareLocal` rejects. The same script compiles and runs on the desktop. The playground
detects the failure and explains it, and the workaround is a list:

```lens
let shapes = new [[Nowhere; Dot 3]]         // works
let shapes = new [Nowhere; Dot 3]           // refused

fun firstOf<T>:T (items:List<T>) -> items[0]  // works
fun firstOf<T>:T (items:T[]) -> items[0]      // refused
```

Lists, dictionaries, tuples, sequences from iterators, function arguments and return values of such
types are all unaffected, and so are arrays of ordinary types like `int[]`. Only the array is.

**Output appears when the script yields.** Everything printed is buffered and shown when the script
awaits or finishes, because a page cannot repaint while a script is holding its only thread. A
script that awaits produces output as it goes; one that does not produces it all at once at the end.

**The first visit downloads about 8 MB**, compressed. The app is published untrimmed on purpose:
LENS resolves types and methods by reflection, so the trimmer cannot see what a visitor's script
is going to name, and trimming would remove the very API surface the playground exists to expose.
The download is cached and immutable after that.

## Samples

`Samples/*.lns` are embedded into the assembly and offered in the toolbar menu. The file name sets
the order and the title: `03-records-and-matching.lns` becomes "Records and matching", third in
the list. Each one compiles and runs - they are worth keeping that way.
