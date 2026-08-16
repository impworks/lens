LENS: Embeddable scripting for .NET [![NuGet][badge-nuget]][nuget-package]
===

Welcome to the homepage for LENS embeddable compiler!

LENS stands for "<b>L</b>anguage for <b>E</b>mbeddable .<b>N</b>ET <b>S</b>cripting".

### A few examples of the syntax

A basic script:

```csharp
let a = 1
let b = 2
print "the result is: {0}" (a + b)
```

A loop:

```csharp
for x in 10..0 do
    println "{0}..." x

println "blastoff!"
```

LINQ queries:

```csharp
let squareSum = 1.to 100
    |> Where x -> x.even()
    |> Select x -> x ** 2
    |> Sum ()
```

Function declaration:

```csharp
using System.Drawing

pure fun dist:double (p1:Point p2:Point) ->
    let x = p1.X - p2.X
    let y = p1.Y - p2.Y
    Math::Sqrt (x ** 2 + y ** 2)

let pA = new Point 1 2
let pB = new Point 10 20
print "The distance is: {0}" (dist pA pB)
```

Custom data structures:

```csharp
record Store
    Name : string
    Stock : int

let stores = new [
    new Store "A" 10
    new Store "B" 42
    new Store "C" 5
]

for s in stores.OrderByDescending (x-> x.Stock) do
    println "Store {0} contains has {1} products in stock" s.Name s.Stock
```

Partial application and function composition:

```csharp
let multiplier = (x:int y:int) -> x * y
let inv = (a:string b:string) -> b + a
let doubler = multiplier 2 _

// compose functions together
let invParse = inv :> int::Parse :> doubler :> (x -> println x)

invParse "1" "2" // 42
```

Pattern matching:

```csharp
fun desribe:string (arr:object[]) ->
    match arr with
        case [] then "empty array"
        case [x:int] when x < 10 then fmt "array with 1 small int ({0})" x
        case [x] then "array with 1 item"
        case [x; y] then "array with 2 items"
        case [x; y; ...z] then fmt "array with {0}, {1} and {2} more items" x y z.Length
```

### Supported frameworks

LENS targets `net47`, `netstandard2.0`, and `net8.0`.

### Why another language?

LENS provides an easy way to compile and execute a script within your application, and manages the interconnection between the app and the script. The language has a light, conscise, and type-safe syntax with rich functional features.

### Oh really?

Why yes indeed! Here's a snippet that shows how to embed the compiler into your application:

```csharp
try
{
    var x = 21;
    var y = 2;
    var result = 0;

    var cp = new LensCompiler();
    cp.RegisterProperty("x", () => x);
    cp.RegisterProperty("y", () => y);
    cp.RegisterProperty("res", () => result, r => result = r);

    var source = "res = x * y";
    var compiled = cp.Compile(source);
    compiled.Run();

    Console.WriteLine("The result is {0}", result);
}
catch(LensCompilerException e)
{
    Console.WriteLine("An error has occured: {0}", e.FullMessage);
}
```

The code above creates the compiler and registers local variables `x`, `y`, and `result` in the script. The body of the script is compiled into a native .NET object that can be invoked several times without recompilation. Finally, the result of the expression is printed out - and guess what the result is!

A script may also `await` at its top level:

```csharp
var source = "await (fetchAsync url)";
var result = await cp.RunAsync(source);
```

`CompileAsync` and `RunAsync` are the asynchronous counterparts of `Compile` and `Run`, and either pair works whatever the script turned out to be: a script that awaits nothing simply runs to completion before the task is handed back. Prefer the asynchronous pair in a UI application - waiting for a script that suspends itself would block the very thread its continuation needs.

### Why might one need an embeddable scripting language?

There are many cases in which your application can benefit from an embeddable scripting language:

* **Tasks automation**

    Write scripts to execute tasks automatically within the application, like processing a batch of images in a graphical editor, backing up databases.
    
* **Formulas support**

    Enable Excel-like formulas in your application, with functions and all kinds of cool features.
    
* **Easy tweaking**

    Embeddable scripting is a much more powerful alternative to config files. Scripts can contain some logic which can be altered without recompiling the entire application. Especially useful in game engines!

### What features does the language support?

The compiler already supports the following features:

* **Full access** to any .NET types and assemblies referenced by your host project
* Import of types, methods and even local variables from host into the script (use `declare` block to validate and enable editor support)
* Declaration of records and functions inside the script
* Local type inference
* Generic functions, records, and types - oh my!
* [Anonymous functions](https://github.com/impworks/lens/wiki/Lambda-expressions) with closures
* [Extension methods](https://github.com/impworks/lens/wiki/Invoking-methods-and-functions#extension-methods) and LINQ
* Async-await (in functions and at the top level of a script), iterators, `Expression<T>`
* String interpolation: `$"a{expr}b"`, `$@"..."`, and format specifiers
* Overloaded operators support
* [Partial function application](https://github.com/impworks/lens/wiki/Partial-application) and [function composition](https://github.com/impworks/lens/wiki/Function-composition)
* Pattern matching (with [awesome regex support](https://github.com/impworks/lens/wiki/Pattern-Matching#9-regex-rule))
* Automatic [memoization](https://github.com/impworks/lens/wiki/Functions#memoization) support
* Shorthand operators
* Basic optimizations like constant unrolling
* Safe mode: certain types or namespaces can be disabled for security reasons

Please refer to the [Wiki](https://github.com/impworks/lens/wiki) for the complete list of features.

### Editor support

There is a language server and a [VS Code extension](editors/vscode/README.md): syntax highlighting
(from the compiler, not from a regular expression), completion, diagnostics as you type, hover,
go-to-definition, find-references, rename and an outline.

```
cd editors/vscode
npm install
npm run build-server
npm run compile
npm run package          # produces lens-lang-5.0.0.vsix
```

Install it with `code --install-extension lens-lang-5.0.0.vsix`, or open `editors/vscode` in VS Code
and press F5 to run it without installing.

The server speaks the language server protocol over stdio, so any editor that can launch
`dotnet lens-language-server.dll` gets the same features. A plugin that would rather host the
language services in-process can reference `Lens.LanguageServer.Core` and skip the protocol.

Since a script's meaning depends on what the *host* registered, and an editor has no host, tell it
with a `declare` block at the top of the file - the same block the compiler checks against the real
host when the script runs.

```
declare
    reference "System.Net.Http"         // a platform assembly, found wherever the runtime keeps it
    reference "./lib/Contoso.Model.dll" // a library of your own, relative to the script
    let customer:Contoso.Model.Customer

use System.Net.Http
let http = new HttpClient ()
```

A `reference` entry actually loads the assembly, so its types can be named, completed and checked -
in the editor and when the script runs. An assembly that is part of the platform is named rather
than pathed: `"System.Net.Http"` and `"System.Net.Http.dll"` both work, and neither ties the script
to the machine it was written on. A reference that does not resolve is a warning and not an error,
because the host may have registered the assembly by itself already.

Contributions are always welcome!

### What NOT to expect

Being designed as an embeddable language, LENS does not support some features that are better implemented in the language of the host application. Here is a list of features that you will *not* see any time soon:

* Unsafe code with pointers
* Checked / unchecked semantics
* Multiple source files support
* Declarations of classes or interfaces
* Access restrictions (`private` / `public`, etc)

### Supporting documentation

- [How to publish NuGet package][publish]

[publish]: [docs/publish.md]

[nuget-package]: https://www.nuget.org/packages/LENS/

[badge-nuget]: https://img.shields.io/nuget/v/LENS.svg
