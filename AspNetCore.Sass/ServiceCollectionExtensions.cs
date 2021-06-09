using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.SaSS
{
    public static class ServiceCollectionExtensions
    {
        public static SassCompilerBuilder CompileSass(this IServiceCollection services, IConfiguration configuration = null)
        {
            services.AddOptions<SassOptions>();

            if (configuration != null)
                services.Configure<SassOptions>(configuration);

            services.AddHostedService<SassCompilerHostedService>();
            return new SassCompilerBuilder(services);
        }
    }
}
