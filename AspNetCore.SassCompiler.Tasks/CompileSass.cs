using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AspNetCore.SassCompiler
{
    public sealed class CompileSass : Task
    {
        #region FIELDS

        private static readonly Regex _compiledFilesRe = new Regex(@"^Compiled (.+?) to (.+).$");

        #endregion FIELDS

        #region PROPERTIES

        public string ConfigLocation { get; set; }

        public string Arguments { get; set; }

        public string Command { get; set; }

        public string Snapshot { get; set; }

        [Output]
        public ITaskItem[] GeneratedFiles { get; set; } = Array.Empty<ITaskItem>();

        #endregion PROPERTIES

        #region METHODS

        #region Private methods

        private IEnumerable<ITaskItem> GenerateSourceTarget(SassCompilerOptions options)
        {
            if (Directory.Exists(options.SourceFolder))
            {
                var compilerStartInfo = new ProcessStartInfo
                {
                    FileName = Command,
                    Arguments = $"{Snapshot} {Arguments} {options.SourceFolder}:{options.TargetFolder} --update",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                var compiler = new Process() { StartInfo = compilerStartInfo };

                compiler.Start();
                compiler.WaitForExit();

                if (compiler.ExitCode != 0)
                {
                    var error = compiler.StandardError.ReadToEnd();
                    Log.LogError($"Error running sass compiler: {error}");
                    yield break;
                }

                var output = compiler.StandardOutput.ReadToEnd();
                var matches = _compiledFilesRe.Matches(output);

                foreach (Match match in matches)
                {
                    var cssFile = match.Groups[2].Value;

                    var generatedFile = new TaskItem(cssFile);
                    yield return generatedFile;
                }
            }
            else if (options.SourceFolder != SassCompilerOptions.DefaultSourceFolder)
            {
                Log.LogError($"Sass source folder {options.SourceFolder} does not exist.");
            }
        }

        private IEnumerable<ITaskItem> GenerateScopedCss(SassCompilerOptions options)
        {
            if (options.GenerateScopedCss != true)
                yield break;

            var directories = options.ScopedCssFolders
                .Where(dir => Directory.Exists(dir))
                .Select(dir => dir)
                .ToArray();

            if (directories.Length == 0)
                yield break;

            var compilerStartInfo = new ProcessStartInfo
            {
                FileName = Command,
                Arguments = $"{Snapshot} {Arguments} {string.Join(" ", directories)} --update --no-source-map",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var compiler = new Process { StartInfo = compilerStartInfo };

            compiler.Start();
            compiler.WaitForExit();

            if (compiler.ExitCode != 0)
            {
                var error = compiler.StandardError.ReadToEnd();
                Log.LogError($"Error running sass compiler: {error}");
                yield break;
            }

            var output = compiler.StandardOutput.ReadToEnd();
            var matches = _compiledFilesRe.Matches(output);

            foreach (Match match in matches)
            {
                var cssFile = match.Groups[2].Value;

                var generatedFile = new TaskItem(cssFile);
                yield return generatedFile;
            }
        }

        #endregion Private methods

        #region Public methods

        public override bool Execute()
        {
            //Debugger.Launch();

            SassCompilerOptions options = SassCompilerOptions.GetInstance(ConfigLocation);
            Arguments = options.Arguments.ToLowerInvariant().Replace("--watch", string.Empty);

            var generatedFiles = new List<ITaskItem>();

            generatedFiles.AddRange(GenerateSourceTarget(options));
            generatedFiles.AddRange(GenerateScopedCss(options));

            GeneratedFiles = generatedFiles.ToArray();

            return true;
        }

        #endregion Public methods

        #endregion METHODS
    }
}
