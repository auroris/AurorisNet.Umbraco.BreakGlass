# Umbraco compatibility

BreakGlass supports Umbraco 17 and 18 on .NET 10 in one NuGet package. It compiles against
17.0.0 and declares `[17.0.0, 19.0.0)` for both `Umbraco.Cms.Api.Management` and
`Umbraco.Cms.Infrastructure`. The consuming site supplies its supported Umbraco version.

`Directory.Build.props` separates the compilation baseline, `UmbracoMinimumVersion`, from
the test site's `UmbracoVersion`, which defaults to 18.3.1. Changing the test version does
not change the package's compilation baseline. Marketplace metadata uses `SemVer` dependency
mode to describe support through the package's declared range.

## API wrappers

The wrappers live in `src/Compatibility` and follow the same pattern as the Drawio package:

- `IUmbracoApi` defines the CMS operations needed for administrator recovery, preparing a
  Debug sign-in and notifying Umbraco of a successful login.
- `UmbracoApi` implements the shared account operations: locating the built-in administrator,
  changing its identity and password, clearing lockout and failed attempts, removing
  two-factor enrolment for configured-password recovery, and recording Debug sign-ins.
- `Umbraco17Api` and `Umbraco18Api` explicitly identify the supported majors. Both currently
  use the shared implementation because these account APIs are compatible.
- `UmbracoCompatibility` selects the adapter using the running CMS version, registers the
  startup notification handler and installs the sign-in services through
  `BreakGlassRegistration`. Unsupported majors have no fallback adapter.

`ConfiguredAdminPassword` coordinates startup and configuration reloads, then calls the
adapter in a service scope. The native sign-in manager and password checker remain thin
integration points: normal sign-in uses Umbraco's checks, and Debug sign-in requires both
`BreakGlass:DebugSignIn` and `Umbraco:CMS:Hosting:Debug`. `DebugSignIn` defaults to `false`
on both supported majors.

Keep shared operations in `UmbracoApi`; place any differing CMS calls in the corresponding
version adapter. Supporting another major requires an explicit adapter, integration tests
and an updated dependency range.

## Verified versions

Local validation on 17 November 2026 used Windows, .NET 10, SQLite and real Umbraco sites
bound to loopback. Each row ran the full suite through the backoffice HTTP login endpoint
and configuration-file reloads, alongside the settings and adapter-selection checks.

| Umbraco version | Result |
|---|---|
| 17.0.0 | 25 passed, 0 failed, 0 skipped |
| 17.8.2 | 25 passed, 0 failed, 0 skipped |
| 18.0.0 | 25 passed, 0 failed, 0 skipped |
| 18.3.1 | 25 passed, 0 failed, 0 skipped |

Coverage includes password recovery at startup and on reload, administrator renaming,
lockout recovery, two-factor removal, rejected passwords, default-off Debug sign-in,
explicit enabling and disabling, and normal authentication after the setting is removed.
The tests also verify the runtime adapter and reject unsupported majors. This validation
uses HTTP integration tests; it does not include browser UI testing or every intervening
Umbraco patch release.

The local NuGet package was built successfully. Inspection confirmed Umbraco assembly
references at `17.0.0.0` and both declared dependency ranges at `[17.0.0, 19.0.0)`.
The marketplace JSON also passed validation against its schema. Older test baselines
reported known NuGet dependency advisories; passing the suite does not resolve them.

## Run the checks

Run these commands from the repository root. The test suite stops each temporary site and
removes its temporary database when it finishes.

```powershell
dotnet build -c Release -p:ContinuousIntegrationBuild=true
dotnet test --project tests/AurorisNet.Umbraco.BreakGlass.Tests -c Release -p:UmbracoVersion=17.0.0 -p:ContinuousIntegrationBuild=true
dotnet test --project tests/AurorisNet.Umbraco.BreakGlass.Tests -c Release -p:UmbracoVersion=17.8.2 -p:ContinuousIntegrationBuild=true
dotnet test --project tests/AurorisNet.Umbraco.BreakGlass.Tests -c Release -p:UmbracoVersion=18.0.0 -p:ContinuousIntegrationBuild=true
dotnet test --project tests/AurorisNet.Umbraco.BreakGlass.Tests -c Release -p:ContinuousIntegrationBuild=true
dotnet pack src/AurorisNet.Umbraco.BreakGlass.csproj -c Release -p:ContinuousIntegrationBuild=true -o artifacts/packages
```

The existing Release workflow runs the full suite on 17.0.0 and the default 18.x test
version before packing. The additional 17.8.2 and 18.0.0 checks above were part of local
compatibility validation. Build warnings are errors except for the NuGet advisory warning
codes already listed in `Directory.Build.props`.
