using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace AspNetCore.SassCompiler
{
    public sealed class CompileSass : Task
    {
        private static readonly Regex _compiledFilesRe = new Regex(@"^Compiled (.+?) to (.+).$", RegexOptions.Multiline);

        public string AppsettingsFile { get; set; }

        public string SassCompilerFile { get; set; }

        public string Command { get; set; }

        private string _snapshot;
        public string Snapshot
        {
            get => _snapshot;
            set => _snapshot = string.IsNullOrWhiteSpace(value) ? "" : $"\"{value}\"";
        }

        public string Configuration { get; set; }

        [Output]
        public ITaskItem[] GeneratedFiles { get; set; } = Array.Empty<ITaskItem>();

        public override bool Execute()
        {
            if (string.IsNullOrWhiteSpace(Command))
            {
                Log.LogWarning("Your OS or CPU architecture is currently not supported.");
                return false;
            }

            var options = GetSassCompilerOptions();
            if (options == null)
                return false;

            var generatedFiles = new List<ITaskItem>();

            generatedFiles.AddRange(GenerateSourceTarget(options));
            generatedFiles.AddRange(GenerateScopedCss(options));

            GeneratedFiles = generatedFiles.ToArray();

            return true;
        }

        private SassCompilerOptions GetSassCompilerOptions()
        {
            var configuration = ReadConfigFile();
            if (configuration == null)
                return null;

            var options = new SassCompilerOptions();

            BindConfiguration(options, configuration);

            if (configuration.TryGetValue("Configurations", out var value) && value is IDictionary<string, object> configOverrides)
            {
                if (!string.IsNullOrEmpty(Configuration) && configOverrides.TryGetValue(Configuration, out value) && value is IDictionary<string, object> overrides)
                {
                    BindConfiguration(options, overrides);
                }
            }

            if (options.ScopedCssFolders == null)
                options.ScopedCssFolders = SassCompilerOptions.DefaultScopedCssFolders;

            if (options.Arguments.Contains("--watch"))
            {
                Log.LogWarning("Cannot use --watch as sass argument when running in MSBuild, use the .AddSassCompiler() method on the IServiceCollection instead.");
                options.Arguments = options.Arguments.Replace("--watch", "");
            }

            if (!options.Arguments.Contains("--style"))
            {
                var style = Configuration == "Debug" ? "expanded" : "compressed";
                Log.LogMessage(MessageImportance.Normal, $"--style argument not provided as sass compiler argument, using --style={style} as default for {Configuration} builds");
                options.Arguments = $"--style={style} {options.Arguments}";
            }

            if (!options.Arguments.Contains("--source-map") && !options.Arguments.Contains("--no-source-map"))
            {
                var sourceMaps = Configuration == "Debug" ? "--source-map" : "--no-source-map";
                Log.LogMessage(MessageImportance.Normal, $"no source map argument was provided as sass compiler argument, using {sourceMaps} as default for {Configuration} builds");
                options.Arguments = $"{sourceMaps} {options.Arguments}";
            }

            return options;
        }

        private IDictionary<string, object> ReadConfigFile()
        {
            if (File.Exists(SassCompilerFile))
            {
                try
                {
                    var text = File.ReadAllText(SassCompilerFile);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var json = SimpleJson.SimpleJson.DeserializeObject(text);
                        if (json is IDictionary<string, object> dict)
                        {
                            if (dict.ContainsKey("SassCompiler") && dict["SassCompiler"] is IDictionary<string, object>)
                            {
                                Log.LogError("Detected 'SassCompiler' key in the sasscompiler.json, this does not work. Remove the 'SassCompiler' key so that the configuration keys are directly on the root object.");
                                return null;
                            }

                            return dict;
                        }
                    }
                    else
                    {
                        Log.LogWarning("sasscompiler.json exists but is empty, ignoring it.");
                    }
                }
                catch (SerializationException ex)
                {
                    Log.LogError("sasscompiler.json is invalid: {0}", ex.Message);
                    return null;
                }
                catch (Exception ex)
                {
                    Log.LogError("Unable to read sasscompiler.json: {0}", ex.ToString());
                    return null;
                }
            }

            if (File.Exists(AppsettingsFile))
            {
                try
                {
                    var text = File.ReadAllText(AppsettingsFile);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        var json = SimpleJson.SimpleJson.DeserializeObject(text);

                        if (json is IDictionary<string, object> root
                            && root.TryGetValue("SassCompiler", out var section)
                            && section is IDictionary<string, object> dict)
                        {
                            return dict;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.LogWarning("Unable to read appsettings.json ({0}), ignoring it.", ex.Message);
                }
            }

            return new Dictionary<string, object>();
        }

        private void BindConfiguration(SassCompilerOptions options, IDictionary<string, object> configuration)
        {
            object value;
            if (configuration.TryGetValue("SourceFolder", out value) && value is string sourceFolder)
                options.SourceFolder = sourceFolder;
            if (configuration.TryGetValue("TargetFolder", out value) && value is string targetFolder)
                options.TargetFolder = targetFolder;
            if (configuration.TryGetValue("Arguments", out value) && value is string arguments)
                options.Arguments = arguments;
            if (configuration.TryGetValue("GenerateScopedCss", out value) && value is bool generateScopedCss)
                options.GenerateScopedCss = generateScopedCss;
            if (configuration.TryGetValue("ScopedCssFolders", out value) && value is IList<object> scopedCssFolders)
                options.ScopedCssFolders = scopedCssFolders.Where(x => x is string).Cast<string>().ToArray();
            if (configuration.TryGetValue("IncludePaths", out value) && value is IList<object> includePaths)
                options.IncludePaths = includePaths.OfType<string>().ToArray();
        }

        private IEnumerable<ITaskItem> GenerateSourceTarget(SassCompilerOptions options)
        {
            if (Directory.Exists(options.SourceFolder))
            {
                var arguments =
                    $"{Snapshot} {options.Arguments} {options.GetLoadPathArguments()} {options.SourceFolder}:{options.TargetFolder} --update";
            
                var (success, output, error) = GenerateCss(arguments);

                if (!success)
                {
                    Log.LogError($"Error running sass compiler: {error}.");
                    yield break;
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    Log.LogWarning(error);
                }

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
            if (!options.GenerateScopedCss)
                yield break;

            var directories = new HashSet<string>();
            foreach (var dir in options.ScopedCssFolders)
            {
                if (Directory.Exists(dir))
                    directories.Add(dir);
            }

            if (directories.Count == 0)
                yield break;

            var arguments = $"{Snapshot} {options.Arguments} {options.GetLoadPathArguments()} {string.Join(" ", directories)} --update";
            
            var (success, output, error) = GenerateCss(arguments);

            if (!success)
            {
                Log.LogError($"Error running sass compiler: {error}.");
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                Log.LogWarning(error);
            }
            
            var matches = _compiledFilesRe.Matches(output);

            foreach (Match match in matches)
            {
                var cssFile = match.Groups[2].Value;

                var generatedFile = new TaskItem(cssFile);
                yield return generatedFile;
            }
        }

        private (bool Success, string Output, string Error) GenerateCss(string arguments)
        {
            var compiler = new Process();
            compiler.StartInfo = new ProcessStartInfo
            {
                FileName = Command,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            string error = null;
            compiler.ErrorDataReceived += (sender, e) =>
            {
                error += e.Data + Environment.NewLine;
            };
            
            compiler.Start();

            compiler.BeginErrorReadLine();
            
            var output = compiler.StandardOutput.ReadToEnd();

            compiler.WaitForExit();

            var success = compiler.ExitCode == 0;

            return (success, output, error);
        }
    }
}
