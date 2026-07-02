using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using AspNetCore.SassCompiler;
using Microsoft.Build.Framework;
using Xunit;

namespace AspNetCore.SassCompiler.Tests;

public class CompileSassWindowsLongPathTests
{
    private static readonly object _currentDirectoryLock = new();

    [Fact]
    public void CreateContext_OnWindowsWithLongResolvedPaths_UsesShortWorkingDirectory()
    {
        // Arrange
        var rootDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var longRelativePath = Path.Combine(
            "obj",
            "Debug",
            "net9.0",
            "scopedcss",
            new string('x', 50),
            new string('y', 50),
            new string('z', 50));
        Directory.CreateDirectory(Path.Combine(rootDirectory, "Styles"));

        try
        {
            // Act
            using var context = SassCliPathHelper.CreateContext(rootDirectory, longRelativePath);

            // Assert
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.NotEqual(rootDirectory, context.WorkingDirectory);
                Assert.True(Directory.Exists(context.WorkingDirectory));
                Assert.Equal("Styles/site.scss", context.CreateUpdateMappingArgument("Styles/site.scss", "wwwroot/css/site.css").Split('"')[1]);
            }
            else
            {
                Assert.Equal(rootDirectory, context.WorkingDirectory);
            }
        }
        finally
        {
            DeleteDirectoryQuietly(rootDirectory);
        }
    }

    [Fact]
    public void Execute_OnWindows_CompilesDeepDirectoryMappingsWithoutSassDirectoryListing()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return;

        var rootDirectory = CreateLongRootDirectory();
        var sourceDirectory = Path.Combine(rootDirectory, "Styles");
        var sourceRelative = Path.Combine("Components", "Feature", "VariantForms", "SubArea", "DeepArea");
        var targetFile = Path.Combine(rootDirectory, "obj", "Debug", "net9.0", "scopedcss", "Components", "Feature", "VariantForms", "SubArea", "DeepArea", "site.css");
        var configFile = Path.Combine(rootDirectory, "sasscompiler.json");

        Directory.CreateDirectory(Path.Combine(sourceDirectory, sourceRelative));
        File.WriteAllText(Path.Combine(sourceDirectory, sourceRelative, "site.scss"), "body { color: black; }");
        File.WriteAllText(configFile,
            "{\n" +
            "  \"Source\": \"Styles/Components\",\n" +
            "  \"Target\": \"obj/Debug/net9.0/scopedcss/Components\",\n" +
            "  \"Arguments\": \"--no-source-map\"\n" +
            "}");

        var sassCommand = SassCompiler.GetSassCommand();
        var task = new CompileSass
        {
            AppsettingsFile = Path.Combine(rootDirectory, "appsettings.json"),
            SassCompilerFile = configFile,
            Command = sassCommand.Filename,
            Snapshot = sassCommand.Snapshot.Trim('"'),
            Configuration = "Debug",
            BuildEngine = new TestBuildEngine()
        };

        var originalDirectory = Directory.GetCurrentDirectory();

        try
        {
            lock (_currentDirectoryLock)
            {
                Directory.SetCurrentDirectory(rootDirectory);

                // Act
                var succeeded = task.Execute();

                // Assert
                Assert.True(succeeded);
                Assert.True(File.Exists(targetFile));
                Assert.Equal("body {\n  color: black;\n}\n", File.ReadAllText(targetFile));
            }
        }
        finally
        {
            lock (_currentDirectoryLock)
            {
                Directory.SetCurrentDirectory(originalDirectory);
            }

            DeleteDirectoryQuietly(rootDirectory);
        }
    }

    private static string CreateLongRootDirectory()
    {
        var baseDirectory = Path.Combine(Path.GetTempPath(), "aspnetcore-sasscompiler-tests", new string('x', 110));
        var rootDirectory = Path.Combine(baseDirectory, new string('y', 40), Path.GetRandomFileName());
        Directory.CreateDirectory(rootDirectory);
        return rootDirectory;
    }

    private static void DeleteDirectoryQuietly(string path)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);

                return;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) when (attempt < 4)
            {
                Thread.Sleep(100);
            }
        }
    }

    private sealed class TestBuildEngine : IBuildEngine
    {
        public bool ContinueOnError => false;

        public int LineNumberOfTaskNode => 0;

        public int ColumnNumberOfTaskNode => 0;

        public string ProjectFileOfTaskNode => string.Empty;

        public void LogErrorEvent(BuildErrorEventArgs e)
        {
        }

        public void LogWarningEvent(BuildWarningEventArgs e)
        {
        }

        public void LogMessageEvent(BuildMessageEventArgs e)
        {
        }

        public void LogCustomEvent(CustomBuildEventArgs e)
        {
        }

        public bool BuildProjectFile(string projectFileName, string[] targetNames, System.Collections.IDictionary globalProperties, System.Collections.IDictionary targetOutputs)
            => true;
    }
}
