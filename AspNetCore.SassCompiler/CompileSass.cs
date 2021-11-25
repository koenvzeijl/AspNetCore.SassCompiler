using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
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
                var json = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });

                if (json.RootElement.TryGetProperty("SassCompiler", out var sassCompiler))
                {
                    if (sassCompiler.TryGetProperty("SourceFolder", out var sourceFolder))
                        options.SourceFolder = sourceFolder.GetString();
                    if (sassCompiler.TryGetProperty("TargetFolder", out var targetFolder))
                        options.TargetFolder = targetFolder.GetString();
                    if (sassCompiler.TryGetProperty("Arguments", out var arguments))
                        options.Arguments = arguments.GetString();
                    if (sassCompiler.TryGetProperty("GenerateScopedCss", out var generateScopedCss))
                        options.GenerateScopedCss = generateScopedCss.GetBoolean();
                    if (sassCompiler.TryGetProperty("ScopedCssFolders", out var scopedCssFolders))
                    {
                        options.ScopedCssFolders = new string[scopedCssFolders.GetArrayLength()];
                        var i = 0;
                        foreach (var folder in scopedCssFolders.EnumerateArray())
                            options.ScopedCssFolders[i++] = folder.GetString();
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
