using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using AspNetCore.SassCompiler.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetCore.SassCompiler
{
    internal sealed class SassCompilerHostedService : IHostedService, IDisposable
    {
        private readonly ILogger<SassCompilerHostedService> _logger;
        private Process _process;

        public SassCompilerHostedService(ILogger<SassCompilerHostedService> logger)
        {
            _logger = logger;
        }

        ~SassCompilerHostedService() => Dispose();

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartProcess();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
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

            _process.KillAllByName();

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

            _logger.LogInformation("Started Sass watch");
        }

        private async void HandleProcessExit(object sender, object args)
        {
            _process.Dispose();
            _process = null;

            _logger.LogWarning("Sass compiler exited, restarting in 1 second.");

            await Task.Delay(1000);
            StartProcess();
        }

        private Process GetSassProcess()
        {
            var options = SassCompilerOptions.GetInstance();
            var rootFolder = Directory.GetCurrentDirectory();

            var command = GetSassCommand();
            if (command.Filename == null)
                return null;

            var directories = new List<string>();
            directories.Add($"\"{Path.Combine(rootFolder, options.SourceFolder)}\":\"{Path.Combine(rootFolder, options.TargetFolder)}\"");
            if (options.GenerateScopedCss == true)
            {
                foreach (var dir in options.ScopedCssFolders)
                {
                    if (dir == options.SourceFolder)
                        continue;

                    if (Directory.Exists(Path.Combine(rootFolder, dir)))
                        directories.Add($"\"{Path.Combine(rootFolder, dir)}\":\"{Path.Combine(rootFolder, dir)}\"");
                }
            }

            var startInfo = new ProcessStartInfo()
            {
                FileName = command.Filename,
                Arguments = $"{command.Snapshot} --error-css --watch {options.Arguments} {string.Join(" ", directories)}",
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };

            return new Process() { StartInfo = startInfo };
        }

        private static (string Filename, string Snapshot) GetSassCommand()
        {
            var attribute = Assembly.GetEntryAssembly().GetCustomAttributes<SassCompilerAttribute>().FirstOrDefault();

            if (attribute != null)
                return (attribute.SassBinary, attribute.SassSnapshot);

            var assemblyLocation = typeof(SassCompilerHostedService).Assembly.Location;

            string exePath, snapshotPath;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                exePath = "runtimes\\win-x64\\src\\dart.exe";
                snapshotPath = "runtimes\\win-x64\\src\\sass.snapshot";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                exePath = "runtimes/linux-x64/sass";
                snapshotPath = null;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                exePath = "runtimes/osx-x64/src/dart";
                snapshotPath = "runtimes/osx-x64/src/sass.snapshot";
            }
            else
                return (null, null);

            var directory = Path.GetDirectoryName(assemblyLocation);
            while (!string.IsNullOrEmpty(directory) && directory != "/")
            {
                if (File.Exists(Path.Combine(directory, exePath)))
                    return (Path.Combine(directory, exePath), snapshotPath == null ? null : "\"" + Path.Combine(directory, snapshotPath) + "\"");

                directory = Path.GetDirectoryName(directory);
            }

            return (null, null);
        }
    }
}
