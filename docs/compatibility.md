# Umbraco compatibility

One build of BreakGlass supports Umbraco 17 and 18. Read this page before you change the
code in `src/Compatibility` or check the package on an Umbraco release. The
[README](../README.md) lists the supported releases and the steps for adding an Umbraco major
version.

## One build for both major versions

The package depends on `Umbraco.Cms.Infrastructure` and `Umbraco.Cms.Api.Management` in the
range `[17.0.0, 19.0.0)`, which `Directory.Packages.props` sets. It compiles against the
lower bound, `UmbracoMinimumVersion` in `Directory.Build.props`, so its code can use only the
APIs that Umbraco 17.0.0 has. A site restores its own Umbraco release.

The tests build a site with a different property, `UmbracoVersion`, which defaults to the
newest supported release. Passing `-p:UmbracoVersion=17.0.0` changes the release the tests
run on and leaves the release the package compiles against as it is.

## Types in src/Compatibility

| Type | Purpose |
|---|---|
| `IUmbracoApi` | The Umbraco operations the package needs: `ApplyAdministratorAsync`, `PrepareDebugSignInAsync` and `NotifyLoginSuccess`, with the `MajorVersion` of the adapter. |
| `UmbracoApi` | The implementation both major versions share. It finds the built-in administrator, sets its username, email address and password, unlocks it, clears its failed sign-in attempts and removes its two-factor enrolment. For a Debug sign-in it lifts a lockout and records the sign-in time. |
| `Umbraco17Api`, `Umbraco18Api` | The adapter for each major version. Both use the shared implementation, because Umbraco 17 and 18 have the same account APIs. |
| `UmbracoCompatibility` | Registers `IUmbracoApi` with the adapter for the running site's major version, the handler that applies the administrator settings when Umbraco has started, and `BreakGlassRegistration`. `ImplementationType` throws `NotSupportedException` for any other major version. |
| `BreakGlassRegistration` | Runs when Umbraco finalizes its service registrations, after every composer. It replaces the four services through which Umbraco resolves the backoffice sign-in manager with `DebugBackOfficeSignInManager`, and wraps the last registered `IBackOfficeUserPasswordChecker` in `DebugPasswordChecker`. |
| `DebugBackOfficeSignInManager` | Derives from Umbraco's `BackOfficeSignInManager`. While Debug sign-in applies, `PasswordSignInAsync` signs in an approved account with any nonempty password. Otherwise it calls Umbraco's implementation. |
| `DebugPasswordChecker` | Accepts any nonempty password while Debug sign-in applies. Otherwise it asks the checker the site registered, or returns `FallbackToDefaultChecker`. |

`ConfiguredAdminPassword`, in `src`, reads the settings at startup and on each configuration
reload and calls `IUmbracoApi.ApplyAdministratorAsync` in a service scope. `DebugSignIn`, in
`src`, decides on each sign-in whether Debug sign-in applies.

Keep an operation that both major versions share in `UmbracoApi`. Put a call that differs in
the adapter of the major version it belongs to, as an override.

## Check a release

Run the tests on a release from the repository root:

```powershell
dotnet test --project tests/AurorisNet.Umbraco.BreakGlass.Tests -c Release -p:UmbracoVersion=17.0.0 -p:ContinuousIntegrationBuild=true
```

`ContinuousIntegrationBuild` turns build warnings into errors, as the workflows do, except
for the NuGet advisory warnings that `Directory.Build.props` lists. To check every supported
major version, run the command once for each release in `UmbracoReleases` in
`Directory.Build.props`. The list starts with the first and the newest release of each major
version, and the daily workflow adds each new release that passes.

The tests start Umbraco sites on SQLite, bound to loopback, and sign in with HTTP requests
to the backoffice login endpoint. They check:

- the configured password at startup and after `appsettings.json` is saved, used exactly as
  written;
- recovery of a locked account with two-factor enrolment;
- a rejected password and an empty password, which both leave the account unchanged;
- the configured username and email address;
- that the log contains no password;
- Debug sign-in: off by default and in debug mode alone, on with both settings, past a
  lockout and two-factor enrolment, off again when either setting changes or is removed;
- that an unknown username and an empty password are refused while Debug sign-in applies;
- the adapter for the running major version, and the error for an unsupported one;
- the settings' defaults and validation.
