using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Security;

namespace BreakGlass.Compatibility;

/// <summary>
/// Puts debug sign-in in front of Umbraco's sign-in when Umbraco finalizes its service registrations, after every
/// composer and the site's own code have registered theirs.
/// </summary>
internal sealed class BreakGlassRegistration : ICollectionBuilder
{
    public void RegisterWith(IServiceCollection services)
    {
        // Keep a password checker the site registered, such as a directory check, behind debug sign-in.
        var inner = services.LastOrDefault(descriptor =>
            descriptor.ServiceType == typeof(IBackOfficeUserPasswordChecker) && !descriptor.IsKeyedService);
        RemoveAll(services, typeof(IBackOfficeUserPasswordChecker));
        services.Add(ServiceDescriptor.Describe(typeof(IBackOfficeUserPasswordChecker),
            provider => new DebugPasswordChecker(provider.GetRequiredService<DebugSignIn>(),
                inner is null ? null : (IBackOfficeUserPasswordChecker)Create(provider, inner)),
            inner?.Lifetime ?? ServiceLifetime.Scoped));

        // Umbraco resolves the backoffice sign-in manager through four services; all of them must reach this one.
        services.AddScoped<DebugBackOfficeSignInManager>();
        Replace<SignInManager<BackOfficeIdentityUser>>(services);
        Replace<Umbraco.Cms.Api.Management.Security.BackOfficeSignInManager>(services);
        Replace<Umbraco.Cms.Web.Common.Security.IBackOfficeSignInManager>(services);
        Replace<ICoreBackOfficeSignInManager>(services);
    }

    private static void Replace<TService>(IServiceCollection services) where TService : class
    {
        RemoveAll(services, typeof(TService));
        services.AddScoped(typeof(TService), provider => provider.GetRequiredService<DebugBackOfficeSignInManager>());
    }

    private static void RemoveAll(IServiceCollection services, Type serviceType)
    {
        for (var index = services.Count - 1; index >= 0; index--)
            if (services[index].ServiceType == serviceType && !services[index].IsKeyedService)
                services.RemoveAt(index);
    }

    private static object Create(IServiceProvider provider, ServiceDescriptor descriptor)
        => descriptor.ImplementationInstance
            ?? descriptor.ImplementationFactory?.Invoke(provider)
            ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
}
