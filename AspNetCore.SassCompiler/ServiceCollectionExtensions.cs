using AspNetCore.SassCompiler;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSassCompiler(this IServiceCollection services)
        {
            services.AddSassCompilerCore();
            services.AddHostedService<SassCompilerHostedService>();
            return services;
        }

        public static IServiceCollection AddSassCompilerCore(this IServiceCollection services)
        {
            services.AddSingleton<ISassCompiler, SassCompiler>();
            return services;
        }
    }
}
