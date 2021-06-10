using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

[assembly: HostingStartup(typeof(AspNetCore.Sass.SassCompilerHostedService))]
namespace AspNetCore.Sass
{
    internal sealed class SassCompilerHostedService : IHostedService, IHostingStartup, IDisposable
    {
        private readonly ILogger<SassCompilerHostedService> _logger;
        private readonly SassOptions _sassOptions;

        private Process _process;

        public SassCompilerHostedService(IOptions<SassOptions> sassOptions, ILogger<SassCompilerHostedService> logger)
        {
            _sassOptions = sassOptions.Value;

            _sassOptions.TargetFolder.Replace('\\','/');
            _sassOptions.SourceFolder.Replace('\\','/');

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
            var binFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            _process = new Process();
            _process.StartInfo.FileName = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "sass" + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".bat" : ""));
            _process.StartInfo.Arguments = $"--error-css --watch {rootFolder}\\{_sassOptions.SourceFolder}:{rootFolder}\\{_sassOptions.TargetFolder}";
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

            _logger.LogInformation("Started NPM watch");
        }

        private async void HandleProcessExit(object sender, object args)
        {
            _process.Dispose();
            _process = null;

            _logger.LogWarning("Sass compiler exited, restarting in 1 second.");

            await Task.Delay(1000);
            StartProcess();
        }

        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((config, services) =>
            {
                services.CompileSass();
            });
        }
    }
}
