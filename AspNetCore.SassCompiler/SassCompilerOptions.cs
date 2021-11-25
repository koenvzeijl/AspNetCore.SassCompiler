namespace AspNetCore.SassCompiler
{
    internal class SassCompilerOptions
    {
        public const string DefaultSourceFolder = "Styles";

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

        public string Arguments { get; set; } = "--style=compressed --error-css";

        public bool GenerateScopedCss { get; set; } = true;

        public string[] ScopedCssFolders { get; set; } = new[] { "Views", "Pages", "Shared", "Components" };
    }
}
