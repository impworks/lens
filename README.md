LENS: Language for Embeddable .NET Scripting
====

Welcome to the homepage for LENS embeddable compiler!


### Why another language?

LENS provides an easy way to compile and execute a script within your application, and manages the interconnection between the app and the script. The language has a light, conscise, and type-safe syntax with rich functional features.

### Oh really?

Why yes indeed! Here's a snippet that shows how to embed the compiler into your application:

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

The code above creates the compiler and registers local variables `x`, `y`, and `result` in the script. The body of the script is compiled into a native .NET object that can be invoked several times without recompilation. Finally, the result of the expression is printed out - and guess what the result is!

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
