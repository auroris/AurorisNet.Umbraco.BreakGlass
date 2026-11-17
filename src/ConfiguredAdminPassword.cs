using System.Threading.Channels;
using BreakGlass.Compatibility;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;

namespace BreakGlass;

/// <summary>
/// Applies the built-in administrator's username, email and password from the <c>BreakGlass</c> settings at startup
/// and whenever configuration reloads, so editing <c>appsettings.json</c> recovers the account on a running site.
/// </summary>
public sealed class ConfiguredAdminPassword(
    IConfiguration configuration,
    IOptionsFactory<BreakGlassSettings> settingsFactory,
    IServiceScopeFactory scopeFactory,
    IRuntimeState runtime,
    ILogger<ConfiguredAdminPassword> logger) : BackgroundService
{
    private readonly SemaphoreSlim _applyLock = new(1, 1);
    private readonly TaskCompletionSource _startupApplied = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // File saves can send several notifications. Process them serially and always read
    // the latest configuration, keeping at most one pending update.
    private readonly Channel<bool> _changes = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropOldest,
    });

    private void QueueUpdate() => _changes.Writer.TryWrite(true);

    internal async Task ApplyAtStartupAsync(CancellationToken cancellationToken)
    {
        if (runtime.Level != RuntimeLevel.Run) return;
        // Run in Umbraco's ordered startup notifications, after the base migrations.
        // Parallel identity writes can invalidate SQLite transactions used during startup.
        await ApplyUpdateAsync(cancellationToken);
        _startupApplied.TrySetResult();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var reload = ChangeToken.OnChange(configuration.GetReloadToken, QueueUpdate);
        await _startupApplied.Task.WaitAsync(stoppingToken);
        await foreach (var change in _changes.Reader.ReadAllAsync(stoppingToken))
            await ApplyUpdateAsync(stoppingToken);
    }

    private async Task ApplyUpdateAsync(CancellationToken cancellationToken)
    {
        await _applyLock.WaitAsync(cancellationToken);
        try
        {
            if (runtime.Level != RuntimeLevel.Run) return;
            try
            {
                await ApplyAsync();
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not apply the configured administrator credentials. Check {UserNameSetting}, {EmailSetting} and {PasswordSetting}, then save the file to try again.",
                    BreakGlassSettings.AdminUserNameKey, BreakGlassSettings.AdminEmailKey, BreakGlassSettings.AdminPasswordKey);
            }
        }
        finally { _applyLock.Release(); }
    }

    private async Task ApplyAsync()
    {
        // The factory binds the configuration as it is now. A cached options monitor could still hold the values from
        // before the reload that queued this update.
        var settings = settingsFactory.Create(Options.DefaultName);
        var password = settings.AdminPassword;
        var username = settings.GetAdminUserName();
        var email = settings.GetAdminEmail();
        if (string.IsNullOrWhiteSpace(password) && username is null && email is null) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IUmbracoApi>().ApplyAdministratorAsync(username, email, password);
    }
}
