using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace BreakGlass.Tests;

/// <summary>
/// A normally composed Umbraco site with SQLite and Kestrel, whose <c>appsettings.json</c> the test writes while
/// it runs. The package arrives through ordinary composer discovery, as on a real site.
/// </summary>
internal sealed class TestSite(WebApplication app, PasswordLog log) : IAsyncDisposable
{
    public const string Email = "breakglass@example.test";
    public const string InstalledPassword = "Installed-only-Test5278!";
    private const string LoginPath = "/umbraco/management/api/v1/security/back-office/login";

    public IServiceProvider Services => app.Services;
    public PasswordLog Log => log;
    public Uri Address { get; } = new(app.Services.GetRequiredService<IServer>()
        .Features.Get<IServerAddressesFeature>()!.Addresses.Single());

    /// <summary>A uniquely named temporary folder for one test's sites, deleted by <see cref="DeleteRoot"/>.</summary>
    public static string CreateRoot() => Directory.CreateTempSubdirectory("breakglass-test-").FullName;

    /// <summary>
    /// Starts a site on <paramref name="root"/>, installing it unattended on the first start. The administrator is
    /// installed as <see cref="Email"/> with <see cref="InstalledPassword"/>.
    /// </summary>
    public static async Task<TestSite> StartAsync(string root, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(TestSite).Assembly.GetName().Name,
            ContentRootPath = root,
            EnvironmentName = "Production",
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        // Only the content root's appsettings.json may set Hosting:Debug and the BreakGlass section, so that saving it
        // changes them: a source added here would override the file.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:umbracoDbDSN"] = $"Data Source={Path.Combine(root, "umbraco.db")};Pooling=False",
            ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            ["Umbraco:CMS:Global:Id"] = "6f0c1e4e-6a39-4b8c-9d4e-0a6d3b7f2c11",
            ["Umbraco:CMS:Global:UseHttps"] = "false",
            ["Umbraco:CMS:Global:VersionCheckPeriod"] = "0",
            ["Umbraco:CMS:WebRouting:ApplicationUrlDetection"] = "FirstRequest",
            ["Umbraco:CMS:TypeFinder:AdditionalAssemblyExclusionEntries:0"] = "xunit.",
            ["Umbraco:CMS:Unattended:InstallUnattended"] = "true",
            ["Umbraco:CMS:Unattended:UpgradeUnattended"] = "true",
            ["Umbraco:CMS:Unattended:UnattendedUserName"] = "Break glass administrator",
            ["Umbraco:CMS:Unattended:UnattendedUserEmail"] = Email,
            ["Umbraco:CMS:Unattended:UnattendedUserPassword"] = InstalledPassword,
        });
        // Use the real discovery path; never call this package's composer directly here.
        builder.CreateUmbracoBuilder().AddBackOffice().AddWebsite().AddComposers().Build();
        var log = new PasswordLog();
        builder.Services.AddSingleton<ILogger<ConfiguredAdminPassword>>(log);
        builder.Services.AddSingleton<TestTwoFactorProvider>();
        builder.Services.AddSingleton<ITwoFactorProvider>(services => services.GetRequiredService<TestTwoFactorProvider>());
        builder.Services.PostConfigure<BackOfficeIdentityOptions>(options =>
            options.Tokens.ProviderMap[TestTwoFactorProvider.Name] = new TokenProviderDescriptor(typeof(TestTwoFactorProvider)));

        var app = builder.Build();
        try
        {
            await app.BootUmbracoAsync();
            app.UseUmbraco().WithMiddleware(u => { u.UseBackOffice(); u.UseWebsite(); })
                .WithEndpoints(u => { u.UseBackOfficeEndpoints(); u.UseWebsiteEndpoints(); });
            await app.StartAsync(cancellationToken);
            // Umbraco installs in a hosted service after the server starts, then raises its started notification.
            var installer = app.Services.GetServices<IHostedService>().OfType<BackgroundService>()
                .SingleOrDefault(service => service.GetType().Name == "UnattendedUpgradeBackgroundService");
            if (installer?.ExecuteTask is { } installation)
                await installation.WaitAsync(TimeSpan.FromMinutes(3), cancellationToken);
            var runtime = app.Services.GetRequiredService<IRuntimeState>();
            var ready = ContentRoutingReady(app.Services);
            var waited = Stopwatch.StartNew();
            while ((runtime.Level != RuntimeLevel.Run || !ready()) && runtime.Level != RuntimeLevel.BootFailed
                && waited.Elapsed < TimeSpan.FromMinutes(1))
                await Task.Delay(100, cancellationToken);
            Assert.True(runtime.Level == RuntimeLevel.Run, runtime.BootFailedException?.ToString() ?? runtime.Level.ToString());
            Assert.True(ready(), "Umbraco did not finish initializing its content caches.");
            return new TestSite(app, log);
        }
        catch
        {
            await app.StopAsync(CancellationToken.None);
            await app.DisposeAsync();
            await Serilog.Log.CloseAndFlushAsync();
            throw;
        }
    }

    /// <summary>
    /// Whether Umbraco has initialized its content caches. Umbraco 18.1 added IContentRoutingReadiness; earlier releases
    /// are ready once the runtime level is Run. Reflection keeps the tests building against every supported release.
    /// </summary>
    private static Func<bool> ContentRoutingReady(IServiceProvider services)
    {
        var type = typeof(IRuntimeState).Assembly.GetType("Umbraco.Cms.Core.Routing.IContentRoutingReadiness");
        if (type is null) return () => true;
        var readiness = services.GetRequiredService(type);
        var isReady = type.GetProperty("IsReady")!;
        return () => (bool)isReady.GetValue(readiness)!;
    }

    /// <summary>Writes the content root's <c>appsettings.json</c>, which a running site reloads.</summary>
    public static Task WriteSettingsAsync(string root, object settings)
        => File.WriteAllTextAsync(Path.Combine(root, "appsettings.json"), JsonSerializer.Serialize(settings));

    /// <summary>Signs in through the backoffice's own login endpoint, as the login form does.</summary>
    public async Task<HttpStatusCode> LoginAsync(string username, string password)
    {
        using var client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
        {
            BaseAddress = Address, Timeout = TimeSpan.FromSeconds(30),
        };
        using var response = await client.PostAsJsonAsync(LoginPath, new { username, password });
        var signedIn = response.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(cookie => cookie.StartsWith("UMB_UCONTEXT=", StringComparison.Ordinal));
        // A success that sets no session cookie would not be a sign-in.
        Assert.Equal(response.StatusCode == HttpStatusCode.OK, signedIn);
        return response.StatusCode;
    }

    /// <summary>The built-in administrator, read fresh from the database.</summary>
    public async Task<BackOfficeIdentityUser> AdministratorAsync()
    {
        using var scope = Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IBackOfficeUserManager>()
            .FindByIdAsync(Constants.Security.SuperUserKey.ToString()))!;
    }

    /// <summary>Whether the administrator's stored hash matches, without the password checker debug sign-in changes.</summary>
    public async Task<bool> HasPasswordAsync(string password)
    {
        var administrator = await AdministratorAsync();
        return administrator.PasswordHash is { } hash
            && Services.GetRequiredService<IPasswordHasher<BackOfficeIdentityUser>>()
                .VerifyHashedPassword(administrator, hash, password) != PasswordVerificationResult.Failed;
    }

    public async Task<(bool Locked, int Failures)> LockoutAsync()
    {
        using var scope = Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IBackOfficeUserManager>();
        var administrator = (await users.FindByIdAsync(Constants.Security.SuperUserKey.ToString()))!;
        return (await users.IsLockedOutAsync(administrator), administrator.AccessFailedCount);
    }

    public async Task<string> TwoFactorProvidersAsync()
    {
        using var scope = Services.CreateScope();
        return string.Join(",", await scope.ServiceProvider.GetRequiredService<ITwoFactorLoginService>()
            .GetEnabledTwoFactorProviderNamesAsync(Constants.Security.SuperUserKey));
    }

    public async Task EnrollTwoFactorAsync()
    {
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ITwoFactorLoginService>().SaveAsync(new Umbraco.Cms.Core.Models.TwoFactorLogin
        {
            UserOrMemberKey = Constants.Security.SuperUserKey, ProviderName = TestTwoFactorProvider.Name,
            Secret = "test-only-secret", Confirmed = true,
        });
    }

    public async Task DisableTwoFactorAsync()
    {
        using var scope = Services.CreateScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<ITwoFactorLoginService>()
            .DisableAsync(Constants.Security.SuperUserKey, TestTwoFactorProvider.Name));
    }

    /// <summary>Polls for up to 15 seconds, for changes that a saved settings file causes in the background.</summary>
    public static async Task WaitUntilAsync(Func<Task<bool>> condition, string what)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            if (await condition()) return;
            await Task.Delay(100);
        }
        Assert.Fail($"Waited 15 seconds for {what}.");
    }

    public async ValueTask DisposeAsync()
    {
        await app.StopAsync(CancellationToken.None);
        await app.DisposeAsync();
        await Serilog.Log.CloseAndFlushAsync();
    }

    /// <summary>Deletes a folder from <see cref="CreateRoot"/>, and nothing else.</summary>
    public static void DeleteRoot(string root)
    {
        var fullRoot = Path.GetFullPath(root);
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!fullRoot.StartsWith(temp, StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(fullRoot).StartsWith("breakglass-test-", StringComparison.Ordinal))
            throw new InvalidOperationException($"Unexpected test directory: {fullRoot}");
        // SQLite can hold the file briefly after the site stops.
        for (var attempt = 0; ; attempt++)
        {
            try { Directory.Delete(fullRoot, recursive: true); return; }
            catch (IOException) when (attempt < 20) { Thread.Sleep(250); }
        }
    }
}

/// <summary>Records what the administrator configurator logs, so a test can check that no password is written.</summary>
internal sealed class PasswordLog : ILogger<ConfiguredAdminPassword>
{
    public ConcurrentQueue<string> Messages { get; } = new();
    public ConcurrentQueue<string> Errors { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception) + (exception is null ? "" : Environment.NewLine + exception);
        Messages.Enqueue(message);
        if (logLevel >= LogLevel.Error) Errors.Enqueue(message);
    }
}

/// <summary>A two-factor provider with a fixed code, so a test can enrol the administrator.</summary>
internal sealed class TestTwoFactorProvider : IUserTwoFactorTokenProvider<BackOfficeIdentityUser>, ITwoFactorProvider
{
    public const string Name = "Break glass test authenticator";
    public const string Code = "123456";
    public string ProviderName => Name;
    public Task<string> GenerateAsync(string purpose, UserManager<BackOfficeIdentityUser> manager, BackOfficeIdentityUser user) => Task.FromResult(Code);
    public Task<bool> ValidateAsync(string purpose, string token, UserManager<BackOfficeIdentityUser> manager, BackOfficeIdentityUser user) => Task.FromResult(token == Code);
    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<BackOfficeIdentityUser> manager, BackOfficeIdentityUser user) => Task.FromResult(true);
    public Task<ISetupTwoFactorModel> GetSetupDataAsync(Guid userOrMemberKey, string secret) => throw new NotSupportedException();
    public bool ValidateTwoFactorPIN(string secret, string token) => token == Code;
    public bool ValidateTwoFactorSetup(string secret, string token) => token == Code;
}
