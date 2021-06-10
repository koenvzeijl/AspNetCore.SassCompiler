using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.Sass
{
    public class SassCompilerBuilder
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="services">The services being configured.</param>
        public SassCompilerBuilder(IServiceCollection services)
            => Services = services;

        /// <summary>
        /// The services being configured.
        /// </summary>
        public virtual IServiceCollection Services { get; }

        /// <summary>
        /// Define or overwrite the configured folder that contains the Sass style files.
        /// </summary>
        /// <param name="sourceFolder">The source folder that contains the Sass style files</param>
        public SassCompilerBuilder FromFolder(string sourceFolder)
        {
            Services.PostConfigure<SassOptions>(sassOptions =>
            {
                sassOptions.SourceFolder = sourceFolder;
            });
            return this;
        }

        /// <summary>
        /// Define or overwrite the configured folder the Sass compiler should write the css files to.
        /// </summary>
        /// <param name="targetFolder">The target folder for the css files</param>
        public SassCompilerBuilder ToFolder(string targetFolder)
        {
            Services.PostConfigure<SassOptions>(sassOptions =>
            {
                sassOptions.TargetFolder = targetFolder;
            });
            return this;
        }
    }
}
