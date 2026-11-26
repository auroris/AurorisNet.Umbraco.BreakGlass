# Developing

Requires the .NET 10 SDK (`global.json`) and PowerShell 7.

```powershell
dotnet build -c Release
dotnet test --project tests/AurorisNet.Umbraco.BreakGlass.Tests -c Release
dotnet test --project tests/AurorisNet.Umbraco.BreakGlass.Tests -c Release -p:UmbracoVersion=17.0.0

# Add an Umbraco release manually (requires sibling ../AurorisNet.Build)
pwsh ../AurorisNet.Build/scripts/Add-UmbracoRelease.ps1 -Releases 18.3.2

# Local package (0.0.0-local)
dotnet pack src/AurorisNet.Umbraco.BreakGlass.csproj -c Release -o artifacts/packages
```

## Runtime and validation details

- `Admin*` settings apply only at `RuntimeLevel.Run`, in order: username/email, then password, then unlock + clear failed attempts + remove 2FA. If username/email or password fails validation, the error is logged and remaining steps stop.
- Startup fails on invalid `DebugSignIn`, invalid `AdminEmail`, or non-email `AdminUserName` when `UsernameIsEmail` is `true`. On live reload, invalid values log an error and disable `DebugSignIn` without crashing the site.
- The package replaces Umbraco's backoffice sign-in manager and wraps `IBackOfficeUserPasswordChecker`; outside `DebugSignIn`, normal sign-in runs unchanged.

## Add an Umbraco major version

1. Update the SDK in `global.json`, workflows, and `Directory.Build.props` if a newer .NET version is needed.
2. Add an adapter (e.g. `Umbraco19Api`) in `src/Compatibility` and wire it into `UmbracoCompatibility.ImplementationType`.
3. Raise the upper bound in `Directory.Packages.props` and `CompatibilityTests`.
4. Add the first release to `UmbracoReleases` in `Directory.Build.props` and update `UmbracoVersion`.
5. Add a matching `<!-- umbraco-releases <major> -->` line and badge update in `README.md`.
6. Test on the new release plus the first and newest release of each supported major version.

## Releases

See the shared [release guide](https://github.com/auroris/AurorisNet.Build/blob/main/docs/releasing.md).
