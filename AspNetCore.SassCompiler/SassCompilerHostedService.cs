using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace AspNetCore.SassCompiler
{
    internal sealed class SassCompilerHostedService : IHostedService, IDisposable
    {
        private readonly ILogger<SassCompilerHostedService> _logger;
        private readonly SassCompilerOptions _options;

        private Process _process;

        public SassCompilerHostedService(IConfiguration configuration, ILogger<SassCompilerHostedService> logger)
        {
            _options = new SassCompilerOptions();
            configuration.GetSection("SassCompiler").Bind(_options);

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
            var rootFolder = Directory.GetCurrentDirectory();

            var command = GetSassCommand();
            if (command.Filename == null)
                return null;

            var directories = new List<string>();
            directories.Add($"\"{rootFolder}/{_options.SourceFolder}\":\"{rootFolder}/{_options.TargetFolder}\"");
            if (_options.GenerateScopedCss)
            {
                foreach (var dir in _options.ScopedCssFolders)
                {
                    if (Directory.Exists($"{rootFolder}/{dir}"))
                        directories.Add($"\"{rootFolder}/{dir}\":\"{rootFolder}/{dir}\"");
                }
            }

            var process = new Process();
            process.StartInfo.FileName = command.Filename;
            process.StartInfo.Arguments = $"{command.Snapshot} --error-css --watch {_options.Arguments} {string.Join(" ", directories)}";
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
            var attribute = Assembly.GetEntryAssembly().GetCustomAttributes<SassCompilerAttribute>().FirstOrDefault();

            if (attribute != null)
                return (attribute.SassBinary, attribute.SassSnapshot);

            var assemblyLocation =  typeof(SassCompilerHostedService).Assembly.Location;

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
                if (File.Exists(Path.Join(directory, exePath)))
                    return (Path.Join(directory, exePath), snapshotPath == null ? null : "\"" + Path.Join(directory, snapshotPath) + "\"");

                directory = Path.GetDirectoryName(directory);
            }

            return (null, null);
        }
    }
}
