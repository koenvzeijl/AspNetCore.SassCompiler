using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
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
        private readonly string _sourceFolder;
        private readonly string _targetFolder;

        private Process _process;

        public SassCompilerHostedService(IConfiguration configuration, ILogger<SassCompilerHostedService> logger)
        {
            _sourceFolder = configuration["SassCompiler:SourceFolder"]?.Replace('\\', '/') ?? "Styles";
            _targetFolder = configuration["SassCompiler:TargetFolder"]?.Replace('\\', '/') ?? "wwwroot/css";

            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartProcess();

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (_process != null)
            {
                _process.Close();
                _process.Dispose();
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _process?.Dispose();
            _process = null;
        }

        private void StartProcess()
        {
            var rootFolder = Directory.GetCurrentDirectory();

            var fileName = GetSassCommand();
            if (fileName == null)
            {
                _logger.LogError("sass command not found, not watching for changes.");
                return;
            }

            _process = new Process();
            _process.StartInfo.FileName = fileName;
            _process.StartInfo.Arguments = $"--error-css --watch \"{rootFolder}/{_sourceFolder}\":\"{rootFolder}/{_targetFolder}\"";
            _process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            _process.StartInfo.CreateNoWindow = true;
            _process.StartInfo.UseShellExecute = false;
            _process.StartInfo.RedirectStandardOutput = true;
            _process.StartInfo.RedirectStandardError = true;
            _process.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();

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

        private string GetSassCommand()
        {
            var attribute = Assembly.GetEntryAssembly().GetCustomAttributes<SassCompilerAttribute>().FirstOrDefault();

            return attribute?.SassBinary;
        }
    }
}
