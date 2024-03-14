using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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

            var processArguments = new StringBuilder();
            processArguments.Append(" --error-css");
            processArguments.Append(" --watch");
            processArguments.Append($" {_options.Arguments}");

            if (_options.IncludePaths?.Length > 0)
            {
                foreach (var includePath in _options.IncludePaths)
                {
                    processArguments.Append($" --load-path={includePath}");
                }
            }

            foreach (var compilation in _options.GetAllCompilations())
            {
                var fullSource = Path.GetFullPath(Path.Combine(rootFolder, compilation.Source));
                var fullTarget = Path.GetFullPath(Path.Combine(rootFolder, compilation.Target));

                if (!Directory.Exists(fullSource) && !File.Exists(fullSource))
                    continue;

                processArguments.Append($" \"{fullSource}\":\"{fullTarget}\"");
            }

            var process = SassCompiler.CreateSassProcess(processArguments.ToString());
            return process;
        }
    }
}
