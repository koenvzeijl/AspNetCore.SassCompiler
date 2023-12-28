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
You can also adjust the default configuration in the appsettings.json or sasscompiler.json, do note that when using `appsettings.json` the configuration needs to be nested under a "SassCompiler" property, but when you're using `sasscompiler.json` the settings should _not_ be nested.

### Available options

| Name              | Default value                              | Description                                                    |
|-------------------|--------------------------------------------|----------------------------------------------------------------|
| SourceFolder      | "Styles"                                   | The folder where all the .scss files reside                    |
| TargetFolder      | "wwwroot/css"                              | The folder to output the generated .css files to               |
| Arguments         | "--error-css"                              | Arguments passed to the dart-sass executable                   |
| GenerateScopedCss | true                                       | Enable/disable support for scoped scss                         |
| ScopedCssFolders  | ["Views", "Pages", "Shared", "Components"] | The folders in which .scss files are considered for scoped css |
| IncludePaths      | []                                         | Add folders to search in when importing modules                |

### Examples

<details open>
<summary>appsettings.json</summary>

```json
{
  "SassCompiler": {
    "SourceFolder": "Styles",
    "TargetFolder": "wwwroot/css",
    "Arguments": "--style=compressed",
    "GenerateScopedCss": true,
    "ScopedCssFolders": ["Views", "Pages", "Shared", "Components"],
    "IncludePaths": [],
    
    // You can override specific options based on the build configuration
    "Configurations": {
      "Debug": { // These options apply only to Debug builds
        "Arguments": "--style=expanded"
      }
    }
  }
}
```
</details>

<details>
<summary>sasscompiler.json</summary>

```json
{
  "SourceFolder": "Styles",
  "TargetFolder": "wwwroot/css",
  "Arguments": "--style=compressed",
  "GenerateScopedCss": true,
  "ScopedCssFolders": ["Views", "Pages", "Shared", "Components"],
  "IncludePaths": [],

  // You can override specific options based on the build configuration
  "Configurations": {
    "Debug": { // These options apply only to Debug builds
      "Arguments": "--style=expanded"
    }
  }
}
```
</details>


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

We recommend adding the `#if DEBUG` statement to only use a watcher during debug mode.

**Note:** The Sass watcher is currently not supported inside of a docker container. This should
only be an issue when you're developing inside of a docker container, running the published
application in docker is supported as the compiler is automatically run during the MSBuild publish
step. See [this](https://github.com/koenvzeijl/AspNetCore.SassCompiler/issues/44) issue for the progress.

## Blazor WASM
If you use this with Blazor WebAssembly and want to customize the settings you need to use the sasscompiler.json, using appsettings.json is not supported.
**The sass watcher is currently not supported for Blazor WebAssembly projects**, the MSBuild task is still available and will compile your scss during build and publish.

## Publish

This library also includes an MSBuild task that runs during the publish of your application. Because of this you don't need to include
the Sass Watcher in your release builds and you can safely add the generated .css files to the .gitignore file as they are regenerated during publish. 

### Alpine linux
If you're publishing your application inside an alpine linux container, you will need to install `gcompat` (using `apk add gcompat`) before running `dotnet build` or `dotnet publish`.
This is needed because the dart runtime which is what the `sass` compiler uses requires this package on alpine linux.

## Examples
To provide you with examples, a configured version of a .NET 5.0 project and a configured .NET 6.0 Blazor app are added in the /Samples folder. Please see the link below for quick access

[.NET Core 3.1](https://github.com/koenvzeijl/AspNetCore.SassCompiler/tree/master/Samples/AspNetCore.SassCompiler.Sample31)

[.NET 5.0](https://github.com/koenvzeijl/AspNetCore.SassCompiler/tree/master/Samples/AspNetCore.SassCompiler.Sample)

[.NET 6.0 / Blazor](https://github.com/koenvzeijl/AspNetCore.SassCompiler/tree/master/Samples/AspNetCore.SassCompiler.BlazorSample)
