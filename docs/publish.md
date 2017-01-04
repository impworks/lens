How to publish NuGet package
============================

First of all, clean and rebuild the project in Release configuration for Any CPU
platform.

Then, execute the following in Visual Studio Command Prompt:

```console
$ rem Note that script uses cmd syntax for the commands. If you use another
$ rem shell, you may have to add some additional escaping to the commands.
$ msbuild -m -p:Configuration=Release -p:Platform="Any CPU" -t:Clean;Rebuild
$ nuget pack Lens\Lens.csproj -Properties Configuration=Release -Version 3.0.0-pre1
```

After that, publish `LENS.<version>.nupkg` file to NuGet.