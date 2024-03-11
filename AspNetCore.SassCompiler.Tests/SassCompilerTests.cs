using System.Text;
using Xunit;

namespace AspNetCore.SassCompiler.Tests;

public class SassCompilerTests
{
    [Fact]
    public async Task CompileAsync_WithoutStreams_Success()
    {
        // Arrange
        var sassCompiler = new SassCompiler();

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

    [Theory]
    [InlineData("--watch")]
    [InlineData("--interactive")]
    public async Task CompileAsync_ThrowsWithInvalidArguments(string argument)
    {
        // Arrange
        var sassCompiler = new SassCompiler();

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
        var sassCompiler = new SassCompiler();

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
        var sassCompiler = new SassCompiler();

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
        var sassCompiler = new SassCompiler();

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
        var sassCompiler = new SassCompiler();

        var input = new MemoryStream(Encoding.UTF8.GetBytes("body { color: black; }"));

        // Act
        var result = await sassCompiler.CompileToStringAsync(input, Array.Empty<string>());

        // Assert
        Assert.Equal("body {\n  color: black;\n}\n", result);
    }
}
