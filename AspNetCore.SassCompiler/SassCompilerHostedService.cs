using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetCore.SassCompiler
{
    internal sealed class SassCompilerHostedService : IHostedService, IDisposable
    {
        private readonly ILogger<SassCompilerHostedService> _logger;
        private readonly SassCompilerOptions _options;

        private Process _process;
        private bool _isStopping = false;

        public SassCompilerHostedService(IConfiguration configuration, ILogger<SassCompilerHostedService> logger)
        {
            _options = CreateSassCompilerOptions(configuration);
            _logger = logger;
        }

        ~SassCompilerHostedService() => Dispose();

        private static SassCompilerOptions CreateSassCompilerOptions(IConfiguration configuration)
        {
            SassCompilerOptions options;

            if (File.Exists(Path.Join(Environment.CurrentDirectory, "sasscompiler.json")))
            {
                var contents = File.ReadAllText(Path.Join(Environment.CurrentDirectory, "sasscompiler.json"));
                options = JsonSerializer.Deserialize<SassCompilerOptions>(contents, new JsonSerializerOptions
                {
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    PropertyNameCaseInsensitive = true,
                });
            }
            else
            {
                options = new SassCompilerOptions();
                configuration.GetSection("SassCompiler").Bind(options);
            }

            if (options.ScopedCssFolders == null)
                options.ScopedCssFolders = SassCompilerOptions.DefaultScopedCssFolders;

            if (options.Arguments.Contains("--watch"))
                options.Arguments = options.Arguments.Replace("--watch", "");

            if (!options.Arguments.Contains("--style"))
                options.Arguments = $"--style=expanded {options.Arguments}";

            if (!options.Arguments.Contains("--source-map") && !options.Arguments.Contains("--no-source-map"))
                options.Arguments = $"--source-map {options.Arguments}";

            return options;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartProcess();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _isStopping = true;

            if (_process != null)
            {
                _process.CloseMainWindow();
                if (!_process.HasExited)
                    _process.Kill();

                _process.Dispose();
                _process = null;
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _isStopping = true;

            if (_process != null)
            {
                _process.CloseMainWindow();
                if (!_process.HasExited)
                    _process.Kill();

                _process.Dispose();
                _process = null;
            }
        }

        private void StartProcess()
        {
            _process = GetSassProcess();
            if (_process == null)
            {
                _logger.LogError("sass command not found, not watching for changes.");
                return;
            }

            _process.EnableRaisingEvents = true;

            _process.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                    _logger.LogInformation(args.Data);
            };
            _process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                    _logger.LogError(args.Data);
            };

            _process.Exited += HandleProcessExit;

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            ChildProcessTracker.AddProcess(_process);

            _logger.LogInformation("Started Sass watch");
        }

        private async void HandleProcessExit(object sender, object args)
        {
            _process.Dispose();
            _process = null;

            if (!_isStopping)
            {
                _logger.LogWarning("Sass compiler exited, restarting in 1 second.");

                await Task.Delay(1000);
                StartProcess();
            }
        }

        private Process GetSassProcess()
        {
            var rootFolder = Directory.GetCurrentDirectory();

            var command = GetSassCommand();
            if (command.Filename == null)
                return null;

            var directories = new HashSet<string>();
            directories.Add($"\"{Path.Join(rootFolder, _options.SourceFolder)}\":\"{Path.Join(rootFolder, _options.TargetFolder)}\"");
            if (_options.GenerateScopedCss)
            {
                foreach (var dir in _options.ScopedCssFolders)
                {
                    if (dir == _options.SourceFolder)
                        continue;

                    if (Directory.Exists(Path.Join(rootFolder, dir)))
                        directories.Add($"\"{Path.Join(rootFolder, dir)}\":\"{Path.Join(rootFolder, dir)}\"");
                }
            }

            var process = new Process();
            process.StartInfo.FileName = command.Filename;
            process.StartInfo.Arguments = $"{command.Snapshot} --error-css --watch {_options.Arguments} {_options.GetLoadPathArguments()} {string.Join(" ", directories)}";
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();

            return process;
        }

        private static (string Filename, string Snapshot) GetSassCommand()
        {
            var attribute = Assembly.GetEntryAssembly()?.GetCustomAttributes<SassCompilerAttribute>().FirstOrDefault();

            if (attribute != null)
                return (attribute.SassBinary, string.IsNullOrWhiteSpace(attribute.SassSnapshot) ? "" : $"\"{attribute.SassSnapshot}\"");

            var assemblyLocation =  typeof(SassCompilerHostedService).Assembly.Location;

            var (exePath, snapshotPath) = GetExeAndSnapshotPath();
            if (exePath == null)
                return (null, null);

            var directory = Path.GetDirectoryName(assemblyLocation);
            while (!string.IsNullOrEmpty(directory) && directory != "/")
            {
                if (File.Exists(Path.Join(directory, exePath)))
                    return (Path.Join(directory, exePath), snapshotPath == null ? null : "\"" + Path.Join(directory, snapshotPath) + "\"");

                directory = Path.GetDirectoryName(directory);
            }

            return (null, null);
        }

        private static (string ExePath, string SnapshotPath) GetExeAndSnapshotPath()
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
                    Architecture.X64 => ("runtimes/linux-x64/sass", null),
                    Architecture.Arm64 => ("runtimes/linux-arm64/sass", null),
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
}
