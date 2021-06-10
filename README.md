# AspNetCore.SassCompiler
[![NuGet Version](https://img.shields.io/nuget/v/AspNetCore.SassCompiler.svg?style=flat)](https://www.nuget.org/packages/AspNetCore.SassCompiler/)

Sass Compiler Library for .NET Core 3.x/5.x. without node

## Installation
The installation of this package is quite simple, you can install this package using NuGet with the following command:

```shell
# Package Manager
PM> Install-Package AspNetCore.SassCompiler

# .NET CLI
dotnet add package AspNetCore.SassCompiler
```

## Configuration
After adding the package, the Sass styles from the SourceFolder (defaults to: Styles) will automatically be compiled into `.css` files in the TargetFolder (defaults to: wwwroot\css) on build. To adjust the source and target folder, please add the following configuration in the appsettings.json:
```json
{
  "SassCompiler": {
    "SourceFolder": "Styles",
    "TargetFolder": "wwwroot\\css"
  }
}
```

## Sass watcher
To use the Sass watcher in your project, you must add the following code to your startup.cs:
```csharp
		public void ConfigureServices(IServiceCollection services) {
#if DEBUG
			services.AddSassCompiler();
#endif
		}
```

I recommend adding the `#if DEBUG` statement to only use a watcher during debug mode.

## Examples
As an example, a configured version of a .NET 5.0 project is added in the /Samples folder. Please see the link below for quick access

[.NET Core 5.0](https://github.com/koenvzeijl/AspNetCore.SassCompiler/tree/master/Samples/AspNetCore.SassCompiler.Sample)