using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.SaSS
{
    public static class ServiceCollectionExtensions
    {
        public static SassCompilerBuilder CompileSass(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<SassOptions>(configuration);
            services.AddHostedService<SassCompilerHostedService>();
            return new SassCompilerBuilder(services);
        }
    }
}
