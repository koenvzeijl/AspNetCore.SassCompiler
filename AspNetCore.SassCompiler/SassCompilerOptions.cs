using System.Collections.Generic;

namespace AspNetCore.SassCompiler
{
    internal class SassCompilerOptions : SassCompilerCompilationOptions
    {
        public const string DefaultSource = "Styles";
        public const string DefaultTarget = "wwwroot/css";
        public static readonly string[] DefaultScopedCssFolders = new[] { "Views", "Pages", "Shared", "Components" };

        public SassCompilerOptions()
        {
            Source = DefaultSource;
            Target = DefaultTarget;
        }

        public List<SassCompilerCompilationOptions> Compilations { get; set; } = null;

        public string Arguments { get; set; } = "--error-css";

        public bool GenerateScopedCss { get; set; } = true;

        public string[] ScopedCssFolders { get; set; } = null;

        public string[] IncludePaths { get; set; } = null;

        public IEnumerable<SassCompilerCompilationOptions> GetAllCompilations()
        {
            var seenSources = new HashSet<string>();

            if (!string.IsNullOrEmpty(Source) && !string.IsNullOrEmpty(Target))
            {
                seenSources.Add(Source);
                Optional ??= Source == DefaultSource && Target == DefaultTarget && (Compilations != null || GenerateScopedCss);
                yield return this;
            }

            if (Compilations != null)
            {
                foreach (var compilation in Compilations)
                {
                    seenSources.Add(compilation.Source);
                    compilation.Optional ??= false;
                    yield return compilation;
                }
            }

            if (GenerateScopedCss)
            {
                foreach (var folder in ScopedCssFolders)
                {
                    if (!seenSources.Add(folder))
                        continue;

                    yield return new SassCompilerCompilationOptions { Source = folder, Target = folder, Optional = true };
                }
            }
        }
    }
}
