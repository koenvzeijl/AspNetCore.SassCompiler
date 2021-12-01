# AspNetCore.SassCompiler
[![NuGet Version](https://img.shields.io/nuget/v/AspNetCore.SassCompiler.svg?style=flat)](https://www.nuget.org/packages/AspNetCore.SassCompiler/)

Sass Compiler Library for .NET Core 3.1/5.x./6.x without node.

## Installation
The installation of this package is quite simple, you can install this package using NuGet with the following command:

```shell
# Package Manager
PM> Install-Package AspNetCore.SassCompiler

# .NET CLI
dotnet add package AspNetCore.SassCompiler
```

## Configuration
After adding the package, the Sass styles from the SourceFolder (defaults to: Styles) will automatically be compiled into `.css` files in the TargetFolder (defaults to: wwwroot\css) on build. 
You can also adjust the default (`--style=compressed`) dart-sass Arguments in the appsettings.json. Scoped CSS is also supported for applications that use blazor for example. This feature is enabled by default and will use the default scoped CSS folders as shown below. To disable this feature, change GenerateScopedCss to false.
To adjust any of the default configuration, please add one or more of the following settings to the appsettings.json:
```json
{
  "SassCompiler": {
    "SourceFolder": "Styles",
    "TargetFolder": "wwwroot/css",
    "Arguments": "--style=compressed"
    "GenerateScopedCss": true,
    "ScopedCssFolders": ["Views", "Pages", "Shared", "Components"]
    }
  }
}
```

## Sass watcher
To use the Sass watcher in your project, you must add the following code to your startup.cs:
```csharp
public void ConfigureServices(IServiceCollection services) 
{
  
#if DEBUG
  services.AddSassCompiler();
#endif

}
```

I recommend adding the `#if DEBUG` statement to only use a watcher during debug mode.

## Examples
To provide you with examples, a configured version of a .NET 5.0 project and a configured .NET 6.0 Blazor app are added in the /Samples folder. Please see the link below for quick access

[.NET 5.0](https://github.com/koenvzeijl/AspNetCore.SassCompiler/tree/master/Samples/AspNetCore.SassCompiler.Sample)

[.NET 6.0 / Blazor](https://github.com/koenvzeijl/AspNetCore.SassCompiler/tree/master/Samples/AspNetCore.SassCompiler.BlazorSample)