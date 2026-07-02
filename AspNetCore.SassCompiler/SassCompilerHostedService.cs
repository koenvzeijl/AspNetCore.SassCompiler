using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Linq;
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
        private IDisposable _pathContext;
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

            _pathContext?.Dispose();
            _pathContext = null;

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

            _pathContext?.Dispose();
            _pathContext = null;
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
            var compilations = _options.GetAllCompilations().ToArray();
            _pathContext?.Dispose();
            var pathContext = SassCliPathHelper.CreateContext(
                rootFolder,
                compilations.SelectMany(compilation => new[] { compilation.Source, compilation.Target })
                    .Concat(_options.IncludePaths ?? Array.Empty<string>())
                    .ToArray());
            _pathContext = pathContext;

            var processArguments = new StringBuilder();
            processArguments.Append(" --error-css");
            processArguments.Append(" --watch");
            processArguments.Append($" {_options.Arguments}");

            if (_options.IncludePaths?.Length > 0)
            {
                foreach (var includePath in _options.IncludePaths)
                {
                    processArguments.Append(pathContext.CreateLoadPathArgument(includePath));
                }
            }

            foreach (var compilation in compilations)
            {
                var fullSource = pathContext.GetFullPath(compilation.Source);
                var fullTarget = pathContext.GetFullPath(compilation.Target);

                if (!Directory.Exists(fullSource) && !File.Exists(fullSource))
                    continue;

                processArguments.Append(pathContext.CreateUpdateMappingArgument(compilation.Source, compilation.Target));
            }

            var process = SassCompiler.CreateSassProcess(processArguments.ToString());
            process.StartInfo.WorkingDirectory = pathContext.WorkingDirectory;
            return process;
        }
    }
}
