LENS: Embeddable scripting for .NET
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
let squareSum = range 1 100
    |> Where (x -> x.even())
    |> Select (x -> x ** 2)
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

// partially apply multiplier
let doubler = multiplier 2 _

// compose functions together
let invParse = inv :> int::Parse :> doubler :> (x -> println x)

invParse "1" "2" // 42
```

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
* Import of types, methods and even local variables from host into the script
* Declaration of records and functions inside the script
* Local type inference
* Anonymous functions with closures
* Extension methods and LINQ
* Overloaded operators support
* Basic optimizations like constant unrolling

Some cool features are on the way:

* Safe mode: certain types or namespaces can be disabled for security reasons
* Pattern matching
* Object initializers
* Partial function application and function composition
* Automatic memoization support
* Shorthand operators
* Attributes

The complete list of expected features (in russian) can be found in the Issues tab.

Contributions are always welcome - especially if you would like to help create a text editor with code suggestions and syntax highlighting!

### What NOT to expect

Being designed as an embeddable language, LENS does not support some features that are better implemented in the language of the host application. Here is a list of features that you will *not* see any time soon:

* Unsafe code with pointers
* Checked / unchecked semantics
* Multiple source files support
* Declarations of classes or interfaces
* Access restrictions (`private` / `public`, etc)
