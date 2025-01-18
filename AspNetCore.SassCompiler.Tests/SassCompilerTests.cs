using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AspNetCore.SassCompiler.Tests;

public class SassCompilerTests
{
    [Fact]
    public async Task CompileAsync_WithoutStreams_Success()
    {
        // Arrange
        var sassCompiler = new SassCompiler(NullLogger<SassCompiler>.Instance);

        var tempDirectory = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            await File.WriteAllTextAsync(Path.Join(tempDirectory, "input"), "body { color: black; }");

            // Act
            await sassCompiler.CompileAsync(new[] { Path.Join(tempDirectory, "input"), Path.Join(tempDirectory, "output"), "--no-source-map" });
            var result = await File.ReadAllTextAsync(Path.Join(tempDirectory, "output"));

            // Assert
            Assert.Equal("body {\n  color: black;\n}\n", result);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Fact(Timeout=1000)]
    public async Task CompileAsync_WithoutStreams_ScssWithDeprecationWarnings_Success()
    {
        // Arrange
        var sassCompiler = new SassCompiler(NullLogger<SassCompiler>.Instance);

        var tempDirectory = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var sb = new StringBuilder("$navbar-bg: #ffffff;$brand-header-color: #ffffff;body { background-color: darken($navbar-bg, 10%); color: lighten($brand-header-color, 10%) }.btn { color: lighten($brand-header-color, 10%)}");
            var testClassCount = 65;
            for(var i = 0; i < testClassCount; i++) {
                sb.AppendFormat(".test{0} {{ background-color: darken($navbar-bg, 10%); color: lighten($brand-header-color, 10%) }}", i);
            }
            await File.WriteAllTextAsync(Path.Join(tempDirectory, "input"), sb.ToString());

            // Act
            await sassCompiler.CompileAsync(new[] { Path.Join(tempDirectory, "input"), Path.Join(tempDirectory, "output"), "--no-source-map" });
            var result = await File.ReadAllTextAsync(Path.Join(tempDirectory, "output")); 

            // Assert
            var expectedSb = new StringBuilder("body {\n  background-color: rgb(229.5, 229.5, 229.5);\n  color: white;\n}\n\n.btn {\n  color: white;\n}\n\n");
            for(var i = 0; i < testClassCount; i++) {
                expectedSb.AppendFormat(".test{0} {{\n  background-color: rgb(229.5, 229.5, 229.5);\n  color: white;\n}}\n", i);
                // don't add additional newline for last item
                if(i < testClassCount-1){
                    expectedSb.Append("\n");
                }
            }

            Assert.Equal(expectedSb.ToString(), result);
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    [Theory]
    [InlineData("--watch")]
    [InlineData("--interactive")]
    public async Task CompileAsync_ThrowsWithInvalidArguments(string argument)
    {
        // Arrange
        var sassCompiler = new SassCompiler(NullLogger<SassCompiler>.Instance);

        var input = new MemoryStream(Encoding.UTF8.GetBytes("body { color: black; }"));

        // Act
        async Task Act() => await sassCompiler.CompileAsync(input, new[] { argument });

        // Assert
        var exception = await Assert.ThrowsAsync<SassCompilerException>(Act);
        Assert.Equal($"The sass {argument} option is not supported.", exception.Message);
        Assert.Null(exception.ErrorOutput);
    }

    [Fact]
    public async Task CompileAsync_ThrowsWithInvalidInput()
    {
        // Arrange
        var sassCompiler = new SassCompiler(NullLogger<SassCompiler>.Instance);

        var input = new MemoryStream(Encoding.UTF8.GetBytes("body { color: black;"));

        // Act
        async Task Act() => await sassCompiler.CompileAsync(input, Array.Empty<string>());

        // Assert
        var exception = await Assert.ThrowsAsync<SassCompilerException>(Act);
        Assert.Equal("Sass process exited with non-zero exit code: 65.", exception.Message);
        Assert.StartsWith("Error: expected \"}\".", exception.ErrorOutput);
    }

    [Fact]
    public async Task CompileAsync_ReturningOutputStream_Success()
    {
        // Arrange
        var sassCompiler = new SassCompiler(NullLogger<SassCompiler>.Instance);

        var input = new MemoryStream(Encoding.UTF8.GetBytes("body { color: black; }"));

        // Act
        var output = await sassCompiler.CompileAsync(input, Array.Empty<string>());
        var result = await new StreamReader(output).ReadToEndAsync();

        // Assert
        Assert.Equal("body {\n  color: black;\n}\n", result);
    }

    [Fact]
    public async Task CompileAsync_WithInputAndOutputStream_Success()
    {
        // Arrange
        var sassCompiler = new SassCompiler(NullLogger<SassCompiler>.Instance);

        var input = new MemoryStream(Encoding.UTF8.GetBytes("body { color: black; }"));
        var output = new MemoryStream();

        // Act
        await sassCompiler.CompileAsync(input, output, Array.Empty<string>());
        var result = Encoding.UTF8.GetString(output.ToArray());

        // Assert
        Assert.Equal("body {\n  color: black;\n}\n", result);
    }

    [Fact]
    public async Task CompileToStringAsync_Success()
    {
        // Arrange
        var sassCompiler = new SassCompiler(NullLogger<SassCompiler>.Instance);

        var input = new MemoryStream(Encoding.UTF8.GetBytes("body { color: black; }"));

        // Act
        var result = await sassCompiler.CompileToStringAsync(input, Array.Empty<string>());

        // Assert
        Assert.Equal("body {\n  color: black;\n}\n", result);
    }

    [Fact(Timeout=1000)]
    public async Task CompileToStringAsync_ScssWithDeprecationWarnings_Success() 
    {
        // Arrange
        var sassCompiler = new SassCompiler(NullLogger<SassCompiler>.Instance);

        var sb = new StringBuilder("$navbar-bg: #ffffff;$brand-header-color: #ffffff;body { background-color: darken($navbar-bg, 10%); color: lighten($brand-header-color, 10%) }.btn { color: lighten($brand-header-color, 10%)}");
        var testClassCount = 65;
        for(var i = 0; i < testClassCount; i++) {
            sb.AppendFormat(".test{0} {{ background-color: darken($navbar-bg, 10%); color: lighten($brand-header-color, 10%) }}", i);
        }

        var input = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));

        // Act
        var result = await sassCompiler.CompileToStringAsync(input, Array.Empty<string>());
        
        // Assert
        var expectedSb = new StringBuilder("body {\n  background-color: rgb(229.5, 229.5, 229.5);\n  color: white;\n}\n\n.btn {\n  color: white;\n}\n\n");
        for(var i = 0; i < testClassCount; i++) {
            expectedSb.AppendFormat(".test{0} {{\n  background-color: rgb(229.5, 229.5, 229.5);\n  color: white;\n}}\n", i);
            // don't add additional newline for last item
            if(i < testClassCount-1){
                expectedSb.Append("\n");
            }
        }
        Assert.Equal(expectedSb.ToString(), result);
    }
}
