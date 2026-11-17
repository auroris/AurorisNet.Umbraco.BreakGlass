using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Configuration;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;

namespace BreakGlass.Compatibility;

internal static class UmbracoCompatibility
{
    internal static Type ImplementationType(int major) => major switch
    {
        17 => typeof(Umbraco17Api),
        18 => typeof(Umbraco18Api),
        _ => throw new NotSupportedException($"BreakGlass supports Umbraco 17 and 18; Umbraco {major} needs its own tested adapter.")
    };

    internal static void Register(IUmbracoBuilder builder)
    {
        builder.Services.AddScoped<IUmbracoApi>(services => (IUmbracoApi)ActivatorUtilities.CreateInstance(services,
            ImplementationType(services.GetRequiredService<IUmbracoVersion>().Version.Major)));
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, ConfiguredAdminPasswordStartup>();
        builder.WithCollectionBuilder<BreakGlassRegistration>();
    }
}

// Umbraco raises this after an unattended installation or upgrade as well as a normal startup.
internal sealed class ConfiguredAdminPasswordStartup(ConfiguredAdminPassword password)
    : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    public Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
        => password.ApplyAtStartupAsync(cancellationToken);
}
