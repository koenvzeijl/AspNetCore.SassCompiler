namespace AspNetCore.SassCompiler
{
    internal class SassCompilerOptions
    {
        public const string DefaultSourceFolder = "Styles";
        public static readonly string[] DefaultScopedCssFolders = new[] { "Views", "Pages", "Shared", "Components" };

        private string _sourceFolder = DefaultSourceFolder;
        public string SourceFolder
        {
            get => _sourceFolder;
            set => _sourceFolder = value?.Replace('\\', '/');
        }

        private string _targetFolder = "wwwroot/css";
        public string TargetFolder
        {
            get => _targetFolder;
            set => _targetFolder = value?.Replace('\\', '/');
        }

        public string Arguments { get; set; } = "--error-css";

        public bool GenerateScopedCss { get; set; } = true;

        public string[] ScopedCssFolders { get; set; } = null;

        public string[] IncludePaths { get; set; } = null;

        public string GetLoadPathArguments()
        {
            if (IncludePaths == null || IncludePaths.Length == 0)
                return "";

            return $"--load-path {string.Join(" --load-path ", IncludePaths)}";
        }
    }
}
