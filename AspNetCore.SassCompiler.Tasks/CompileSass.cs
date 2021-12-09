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
        private static readonly Regex _compiledFilesRe = new Regex(@"^Compiled (.+?) to (.+).$");

        public string AppsettingsFile { get; set; }

        public string Command { get; set; }

        public string Snapshot { get; set; }

        [Output]
        public ITaskItem[] GeneratedFiles { get; set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            var options = GetSassCompilerOptions();

            var generatedFiles = new List<ITaskItem>();

            generatedFiles.AddRange(GenerateSourceTarget(options));
            generatedFiles.AddRange(GenerateScopedCss(options));

            GeneratedFiles = generatedFiles.ToArray();

            return true;
        }

        private SassCompilerOptions GetSassCompilerOptions()
        {
            var options = new SassCompilerOptions();

            if (File.Exists(AppsettingsFile))
            {
                var text = File.ReadAllText(AppsettingsFile);
                var json = SimpleJson.SimpleJson.DeserializeObject(text);

                if (json is IDictionary<string, object> root && root.TryGetValue("SassCompiler", out var value))
                {
                    if (value is IDictionary<string, object> sassCompiler)
                    {
                        if (sassCompiler.TryGetValue("SourceFolder", out value) && value is string sourceFolder)
                            options.SourceFolder = sourceFolder;
                        if (sassCompiler.TryGetValue("TargetFolder", out value) && value is string targetFolder)
                            options.TargetFolder = targetFolder;
                        if (sassCompiler.TryGetValue("Arguments", out value) && value is string arguments)
                            options.Arguments = arguments;
                        if (sassCompiler.TryGetValue("GenerateScopedCss", out value) && value is bool generateScopedCss)
                            options.GenerateScopedCss = generateScopedCss;
                        if (sassCompiler.TryGetValue("ScopedCssFolders", out value) && value is IList<object> scopedCssFolders)
                            options.ScopedCssFolders = scopedCssFolders.Where(x => x is string).Cast<string>().ToArray();
                    }
                }
            }

            return options;
        }

        private IEnumerable<ITaskItem> GenerateSourceTarget(SassCompilerOptions options)
        {
            if (Directory.Exists(options.SourceFolder))
            {
                var compiler = new Process();
                compiler.StartInfo = new ProcessStartInfo
                {
                    FileName = Command,
                    Arguments = $"{Snapshot} {options.Arguments} {options.SourceFolder}:{options.TargetFolder} --update",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

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
                Log.LogError($"Sass source folder {options.SourceFolder} does not exist");
            }
        }

        private IEnumerable<ITaskItem> GenerateScopedCss(SassCompilerOptions options)
        {
            if (!options.GenerateScopedCss)
                yield break;

            var directories = new List<string>();
            foreach (var dir in options.ScopedCssFolders)
            {
                if (Directory.Exists(dir))
                    directories.Add(dir);
            }

            if (directories.Count <= 0)
                yield break;

            var compiler = new Process();
            compiler.StartInfo = new ProcessStartInfo
            {
                FileName = Command,
                Arguments = $"{Snapshot} {options.Arguments} {string.Join(" ", directories)} --update --no-source-map",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

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
    }
}
