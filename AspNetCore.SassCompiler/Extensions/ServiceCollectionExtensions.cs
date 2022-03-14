using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.SassCompiler.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSassCompiler(this IServiceCollection services)
        {
            services.AddHostedService<SassCompilerHostedService>();
            return services;
        }
    }
}
