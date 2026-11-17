using BreakGlass.Compatibility;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace BreakGlass;

/// <summary>Adds the configured administrator password and debug sign-in to an Umbraco site.</summary>
public sealed class BreakGlassComposer : IComposer
{
    /// <summary>
    /// Registers the settings and the administrator configurator now, and replaces the sign-in services once every
    /// composer has registered its own.
    /// </summary>
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddOptions<BreakGlassSettings>()
            .BindConfiguration(BreakGlassSettings.SectionName)
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BreakGlassSettings>, BreakGlassSettingsValidator>());
        builder.Services.TryAddSingleton<DebugSignIn>();
        builder.Services.TryAddSingleton<ConfiguredAdminPassword>();
        builder.Services.AddHostedService(services => services.GetRequiredService<ConfiguredAdminPassword>());
        UmbracoCompatibility.Register(builder);
    }
}
