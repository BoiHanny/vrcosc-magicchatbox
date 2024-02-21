using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace MagicChatboxV2.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddModules(this IServiceCollection services, Assembly assembly)
        {
            // Find all types in the assembly that implement IModule and are class types
            var moduleTypes = assembly.GetTypes()
                .Where(t => typeof(UIVM.Models.IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            // Register each found module type with the services collection
            foreach (var type in moduleTypes)
            {
                services.AddTransient(typeof(UIVM.Models.IModule), type);
            }

            return services;
        }
    }
}
