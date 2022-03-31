using AspNetCore.SassCompiler;

namespace Microsoft.Extensions.DependencyInjection
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
