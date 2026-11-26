# Break Glass

Emergency backoffice recovery for Umbraco 17 and 18. Reset and unlock the built-in administrator from configuration, or enable a debug-only sign-in bypass.

![MIT license](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/license-mit.svg)
![.NET 10](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/dotnet-10.svg)
![Umbraco 17 and 18](https://raw.githubusercontent.com/auroris/AurorisNet.Build/main/badges/umbraco-17-18.svg)

## Install

```powershell
dotnet add package AurorisNet.Umbraco.BreakGlass
```

Registers automatically via a composer—no code changes needed.

## Settings

Configured under the `BreakGlass` section (or environment variables such as `BreakGlass__AdminPassword`). The `Admin*` settings target the built-in administrator (user ID `-1`) on startup and whenever configuration reloads.

| Setting | Default | Effect |
|---|---|---|
| `AdminPassword` | empty | Sets the admin password (through Umbraco's password rules), unlocks the account, clears failed attempts, and removes 2FA. |
| `AdminUserName` | empty | Updates the admin username (must be an email if `UsernameIsEmail` is `true`). |
| `AdminEmail` | empty | Updates the admin email address. |
| `DebugSignIn` | `false` | When **both** this and `Umbraco:CMS:Hosting:Debug` are `true`, any non-empty password signs in as any enabled backoffice user, bypassing lockout and 2FA. |

> **Warning:** `DebugSignIn` bypasses password and 2FA checks for all enabled backoffice accounts, including administrators. Only turn it on when backoffice access is restricted to trusted operators, and turn it off immediately afterward.

### Recovering a locked-out admin

1. Set `BreakGlass:AdminPassword` in `appsettings.json` (or via environment variable) and save.
2. Sign in as the administrator.
3. Remove `BreakGlass:AdminPassword` before changing your password or re-enrolling 2FA—while it stays set, every restart or config reload reapplies it and removes 2FA.

## Supported versions

- Umbraco 18: <!-- umbraco-releases 18 -->18.0.0 to 18.3.2<!-- /umbraco-releases --> (.NET 10)
- Umbraco 17: <!-- umbraco-releases 17 -->17.0.0 to 17.8.2<!-- /umbraco-releases --> (.NET 10)

## Documentation

- [Umbraco compatibility](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/docs/compatibility.md)
- [Developing and releases](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/docs/developing.md)
- [Security policy](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/SECURITY.md)

## License

[MIT](https://github.com/auroris/AurorisNet.Umbraco.BreakGlass/blob/main/LICENSE)
