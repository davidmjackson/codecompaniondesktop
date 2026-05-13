# Code Companion Release Checklist

Use this checklist for Milestone 9 release preparation. It covers the paired
release of Code Companion Desktop and Code Companion Voice.

## Release Sources

Target production sources:

- Code Companion Desktop: GitHub Releases in the Code Companion Desktop
  repository.
- Code Companion Voice: VS Code Marketplace.

Current development sources:

- Code Companion Desktop: local installer from
  `artifacts\installer\CodeCompanionDesktopSetup-<version>.exe`.
- Code Companion Voice: local VSIX from
  `D:\Development\CodeCompanionVoice\code-companion-voice-<version>.vsix`.

## Version Gates

Before packaging:

- Choose one release version for Code Companion Desktop.
- Set the Desktop installer `AppVersion`.
- Confirm the Desktop assembly/product version metadata in
  `CodeCompanionDesktop.csproj` matches the installer `AppVersion`.
- Confirm `GET /health` reports the expected Desktop `appVersion`.
- Choose the Code Companion Voice extension version.
- Update `package.json` and `package-lock.json`.
- Confirm the extension is no longer marked with development-only Marketplace
  metadata before public publishing. Current known blockers are:
  - `publisher` is `local`.
  - `private` is `true`.

## Desktop Release Build

From PowerShell in `D:\Development\CodeCompanionDesktop`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-release-package.ps1 -AppVersion <version>
```

Expected outputs:

```text
artifacts\installer\CodeCompanionDesktopSetup-<version>.exe
artifacts\checksums\CodeCompanionDesktopSetup-<version>.exe.sha256
artifacts\release-notes\desktop-<version>.md
```

Required checks:

- `dotnet build CodeCompanionDesktop.sln`
- `dotnet test CodeCompanionDesktop.sln --no-build`
- `git diff --check`
- Release package script completes without errors.
- Installer launches the installed app from:
  `%LOCALAPPDATA%\Programs\Code Companion Desktop\CodeCompanionDesktop.exe`.
- `Invoke-RestMethod http://127.0.0.1:47321/health` returns `status: ok` and
  `bridge: listening`.

The lower-level installer build remains available for local development
iterations:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1 -AppVersion <version>
```

## Desktop GitHub Release

Release artifact:

```text
CodeCompanionDesktopSetup-<version>.exe
```

Release notes must include:

- Version.
- Installer filename.
- SHA256 checksum.
- Minimum Windows and .NET/runtime assumptions.
- Fresh-install verification summary.
- Known limitations.

Draft release command, once release publication is ready:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\draft-github-release.ps1 -AppVersion <version>
```

The command above is a dry run by default. To create the draft GitHub Release:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\draft-github-release.ps1 -AppVersion <version> -Create
```

Do not mark the GitHub Release as final until the fresh-install verification
below passes from the release asset.

## Voice Extension Package

From `D:\Development\CodeCompanionVoice`:

```bash
npm install
npm run compile
npm test
find dist -name '*.js' -print0 | xargs -0 -n1 node --check
git diff --check
npm audit --omit=dev
npm run package:vsix
```

Expected output:

```text
code-companion-voice-<version>.vsix
```

Required checks:

- Package version matches the intended extension release.
- `package.json` uses the real Marketplace publisher.
- `package.json` is not marked `private` for a public Marketplace release.
- Extension README points users to Code Companion Desktop for provider keys.
- The default `desktopBridge.installUrl` points to the Desktop GitHub Releases
  latest URL.

## Voice Marketplace Release

Marketplace release prerequisites:

- Marketplace publisher account is configured.
- Personal access token or publishing credential is available outside the repo.
- Marketplace listing text, icon, license, and repository metadata are final.
- The extension has been tested from the generated VSIX in both Windows and WSL
  extension hosts.

Draft publish command, once Marketplace publication is ready:

```bash
npx vsce publish
```

If the release is not ready for public Marketplace publication, install the
local VSIX instead:

```powershell
& "$env:LOCALAPPDATA\Programs\Microsoft VS Code\bin\code.cmd" --install-extension "D:\Development\CodeCompanionVoice\code-companion-voice-<version>.vsix" --force
```

For WSL extension-host testing:

```bash
code --install-extension /mnt/d/Development/CodeCompanionVoice/code-companion-voice-<version>.vsix --force
```

## Fresh-Install Verification

Perform this from a clean user flow after installing from the intended release
artifacts:

1. Install Code Companion Desktop.
2. Launch Code Companion Desktop from the installer, Start Menu, or desktop
   shortcut.
3. Confirm the running process path is:
   `%LOCALAPPDATA%\Programs\Code Companion Desktop\CodeCompanionDesktop.exe`.
4. Confirm bridge health with:
   `Invoke-RestMethod http://127.0.0.1:47321/health`.
5. Save or confirm the ElevenLabs API key in Code Companion Desktop.
6. Install Code Companion Voice into the active VS Code extension host.
7. Open a new VS Code window if reloading the current window would lose chat
   context.
8. Run `Code Companion Voice: Open Panel`.
9. Confirm the panel shows `Desktop Test` and bridge diagnostics only.
10. Click `Desktop Test`.
11. Approve the pending VS Code client in the Desktop Client Pairing panel.
12. Click `Desktop Test` again.
13. Confirm speech is heard through Code Companion Desktop.
14. Confirm Code Companion Voice output includes `candidate spoken`.

Repeat the extension-host part from:

- A normal Windows VS Code window.
- A WSL Remote VS Code window.

## Rollback Notes

- Desktop can be uninstalled from Windows Apps and Features.
- Desktop user settings are under `%APPDATA%\CodeCompanionDesktop`.
- Provider keys are stored in Windows Credential Manager, not the install
  folder.
- VS Code extension rollback should install the previous Marketplace version or
  previous local VSIX into the same extension host that was upgraded.
