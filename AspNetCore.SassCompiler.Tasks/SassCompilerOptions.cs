using System;
using System.IO;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;

namespace AspNetCore.SassCompiler
{
    public sealed class SassCompilerOptions
    {
        #region FIELDS

        private static SassCompilerOptions _instance;

        private const string _configName = "sasscompiler";
        private readonly string _defaultConfigFileName = $"{_configName}.json";
        private static string _configLocation;

        private string _sourceFolder = DefaultSourceFolder;
        private string _targetFolder = "wwwroot/css";

        public const string DefaultSourceFolder = "Styles";

        #endregion FIELDS

        #region PROPERTIES

        public string TaskBuildEnvironment { get; set; } = string.Empty;

        public string SourceFolder
        {
            get => _sourceFolder;
            set => _sourceFolder = value?.Replace('\\', '/');
        }

        public string TargetFolder
        {
            get => _targetFolder;
            set => _targetFolder = value?.Replace('\\', '/');
        }

        public string Arguments { get; set; } = "--error-css --style=compressed --no-source-map";

        public bool? GenerateScopedCss { get; set; } = true;

        public string[] ScopedCssFolders { get; set; } = new[] { "Views", "Pages", "Shared", "Components" };

        #endregion PROPERTIES

        #region METHODS

        #region Private methods

        private SassCompilerOptions GetConfig()
        {
            var currentConfig = _instance ?? new SassCompilerOptions();
            var defaultConfig = ReadConfig(_defaultConfigFileName);

            MergeConfig(defaultConfig, currentConfig);

            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? defaultConfig.TaskBuildEnvironment;
            var environmentConfigFile = $"{_configName}.{environment}.json";

            if (!string.IsNullOrWhiteSpace(environment)
                && File.Exists(GetConfigFilePath(environmentConfigFile)))
            {
                SassCompilerOptions environmentConfig = ReadConfig(environmentConfigFile);
                MergeConfig(environmentConfig, currentConfig);
            }

            return currentConfig;
        }

        private SassCompilerOptions ReadConfig(string fileName)
        {
            var configFilePath = GetConfigFilePath(fileName);

            if (!File.Exists(configFilePath))
                return _instance;

            using (var sr = new StreamReader(configFilePath, Encoding.UTF8))
            {
                var jsonString = sr.ReadToEnd();
                return JsonConvert.DeserializeObject<SassCompilerOptions>(jsonString);
            }
        }

        private void MergeConfig(SassCompilerOptions fromConfig, SassCompilerOptions toConfig)
        {
            if (fromConfig == null || toConfig == null)
                return;

            if (!string.IsNullOrWhiteSpace(fromConfig.SourceFolder))
                toConfig.SourceFolder = fromConfig.SourceFolder;

            if (!string.IsNullOrWhiteSpace(fromConfig.TargetFolder))
                toConfig.TargetFolder = fromConfig.TargetFolder;

            if (!string.IsNullOrWhiteSpace(fromConfig.Arguments))
                toConfig.Arguments = fromConfig.Arguments;

            if (fromConfig.GenerateScopedCss != null)
                toConfig.GenerateScopedCss = fromConfig.GenerateScopedCss;

            if (fromConfig.ScopedCssFolders?.Length > 0)
                toConfig.ScopedCssFolders = fromConfig.ScopedCssFolders;
        }

        private void CreateDefaultConfigFile()
        {
            string defaultConfigFilePath = GetConfigFilePath(_defaultConfigFileName);

            if (File.Exists(defaultConfigFilePath))
                return;

            var jsonConfig = JsonConvert.SerializeObject(_instance, Formatting.Indented);

            using (StreamWriter sw = new StreamWriter(defaultConfigFilePath))
            {
                sw.WriteLine(jsonConfig);
            }
        }

        private string GetConfigFilePath(string fileName)
        {
            return Path.Combine(_configLocation, fileName);
        }

        #endregion Private methods

        #region Public methods

        public static SassCompilerOptions GetInstance(string configLocation = null)
        {
            _configLocation = configLocation ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (_instance == null)
            {
                _instance = new SassCompilerOptions();
                _instance.CreateDefaultConfigFile();
            }

            _instance = _instance.GetConfig();

            return _instance;
        }

        #endregion Public methods

        #endregion METHODS
    }
}
