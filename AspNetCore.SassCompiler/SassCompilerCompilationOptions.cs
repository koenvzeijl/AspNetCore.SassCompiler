namespace AspNetCore.SassCompiler
{
    internal class SassCompilerCompilationOptions
    {
        private string _source;
        public string Source
        {
            get => _source;
            set => _source = value?.Replace('\\', '/');
        }

        private string _target;
        public string Target
        {
            get => _target;
            set => _target = value?.Replace('\\', '/');
        }

        public bool? Optional { get; set; }
    }
}
