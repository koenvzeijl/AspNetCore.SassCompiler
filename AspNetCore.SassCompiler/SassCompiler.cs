using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AspNetCore.SassCompiler;

public interface ISassCompiler
{
    Task CompileAsync(string[] args);

    Task<Stream> CompileAsync(Stream input, string[] args);

    Task CompileAsync(Stream input, Stream output, string[] args);

    Task<string> CompileToStringAsync(Stream input, string[] args);
}

internal class SassCompiler : ISassCompiler
{
    private readonly ILogger<SassCompiler> _logger;

    public SassCompiler(ILogger<SassCompiler> logger)
    {
        _logger = logger;
    }

    public async Task CompileAsync(string[] args)
    {
        var escapedArgs = args.Length > 0 ? '"' + string.Join("\" \"", args) + '"' : "";
        ValidateArgs(escapedArgs);
        var process = CreateSassProcess(escapedArgs);
        if (process == null)
            throw new SassCompilerException("Sass executable not found");

        var errorOutput = new MemoryStream();

        _logger.LogDebug("Executing sass command: {SassCommand}", $"{process.StartInfo.FileName} {process.StartInfo.Arguments}");

        process.Start();
        ChildProcessTracker.AddProcess(process);
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
        var errorTask = process.StandardError.BaseStream.CopyToAsync(errorOutput);
        await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync());

        if (process.ExitCode != 0)
        {
            var errorOutputText = Encoding.UTF8.GetString(errorOutput.ToArray());
            throw new SassCompilerException($"Sass process exited with non-zero exit code: {process.ExitCode}.", errorOutputText);
        }

        process.Dispose();
    }

    public async Task<Stream> CompileAsync(Stream input, string[] args)
    {
        var output = new MemoryStream();
        await CompileAsync(input, output, args);

        output.Position = 0;
        return output;
    }

    public async Task CompileAsync(Stream input, Stream output, string[] args)
    {
        var escapedArgs = args.Length > 0 ? '"' + string.Join("\" \"", args) + '"' : "";
        ValidateArgs(escapedArgs);
        var process = CreateSassProcess(escapedArgs + " --stdin");
        if (process == null)
            throw new SassCompilerException("Sass executable not found");

        process.StartInfo.RedirectStandardInput = true;

        var errorOutput = new MemoryStream();

        _logger.LogDebug("Executing sass command: {SassCommand}", $"{process.StartInfo.FileName} {process.StartInfo.Arguments}");

        process.Start();
        ChildProcessTracker.AddProcess(process);
        await input.CopyToAsync(process.StandardInput.BaseStream);
        await process.StandardInput.DisposeAsync();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(output);
        var errorTask = process.StandardError.BaseStream.CopyToAsync(errorOutput);
        await Task.WhenAll(outputTask, errorTask, process.WaitForExitAsync());

        if (process.ExitCode != 0)
        {
            var errorOutputText = Encoding.UTF8.GetString(errorOutput.ToArray());
            throw new SassCompilerException($"Sass process exited with non-zero exit code: {process.ExitCode}.", errorOutputText);
        }

        process.Dispose();
    }

    public async Task<string> CompileToStringAsync(Stream input, string[] args)
    {
        using var output = new MemoryStream();
        await CompileAsync(input, output, args);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static void ValidateArgs(string args)
    {
        if (args.Contains("--watch"))
            throw new SassCompilerException("The sass --watch option is not supported.");

        if (args.Contains("--interactive"))
            throw new SassCompilerException("The sass --interactive option is not supported.");
    }

    internal static Process CreateSassProcess(string arguments)
    {
        var command = GetSassCommand();
        if (command.Filename == null)
            return null;

        if (!string.IsNullOrEmpty(command.Snapshot))
            arguments = $"{command.Snapshot} {arguments}";

        var process = new Process();
        process.StartInfo.FileName = command.Filename;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
        process.StartInfo.CreateNoWindow = true;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();

        return process;
    }

    internal static (string Filename, string Snapshot) GetSassCommand()
    {
        var (exePath, snapshotPath) = GetExeAndSnapshotPath();

        var attribute = Assembly.GetEntryAssembly()?.GetCustomAttributes<SassCompilerAttribute>().FirstOrDefault();

        if (attribute != null)
        {
            if (exePath != null)
            {
                // When the runtime compiler is used, the machine the code is compiled on can be different from the 
                // machine it runs on. So we check whether the filepath ends with the expected path for this platform,
                // if not we fall back to searching in the assembly location.
                if (attribute.SassBinary.EndsWith(exePath)
                    && attribute.SassSnapshot.EndsWith(snapshotPath))
                {
                    return (attribute.SassBinary, string.IsNullOrWhiteSpace(attribute.SassSnapshot) ? "" : $"\"{attribute.SassSnapshot}\"");
                }
            }
            else
            {
                return (attribute.SassBinary, string.IsNullOrWhiteSpace(attribute.SassSnapshot) ? "" : $"\"{attribute.SassSnapshot}\"");
            }
        }

        if (exePath == null)
            return (null, null);

        var assemblyLocation =  typeof(SassCompilerHostedService).Assembly.Location;

        var directory = Path.GetDirectoryName(assemblyLocation);
        while (!string.IsNullOrEmpty(directory) && directory != "/")
        {
            if (File.Exists(Path.Join(directory, exePath)))
                return (Path.Join(directory, exePath), snapshotPath == null ? null : "\"" + Path.Join(directory, snapshotPath) + "\"");

            directory = Path.GetDirectoryName(directory);
        }

        return (null, null);
    }

    internal static (string ExePath, string SnapshotPath) GetExeAndSnapshotPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => ("runtimes\\win-x64\\src\\dart.exe", "runtimes\\win-x64\\src\\sass.snapshot"),
                Architecture.Arm64 => ("runtimes\\win-x64\\src\\dart.exe", "runtimes\\win-x64\\src\\sass.snapshot"),
                _ => (null, null),
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => ("runtimes/linux-x64/src/dart", "runtimes/linux-x64/src/sass.snapshot"),
                Architecture.Arm64 => ("runtimes/linux-arm64/src/dart", "runtimes/linux-x64/src/sass.snapshot"),
                _ => (null, null),
            };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.OSArchitecture switch
            {
                Architecture.X64 => ("runtimes/osx-x64/src/dart", "runtimes/osx-x64/src/sass.snapshot"),
                Architecture.Arm64 => ("runtimes/osx-arm64/src/dart", "runtimes/osx-arm64/src/sass.snapshot"),
                _ => (null, null),
            };
        }

        return (null, null);
    }
}
