# Reinstall Code Companion Desktop From Scratch

How to rebuild and reinstall Code Companion Desktop on a clean Windows machine
using nothing but this GitHub repository.

Code Companion is **two installs from two repositories**:

| Component | Repository | Installed as |
| --- | --- | --- |
| Code Companion Desktop (this repo) | <https://github.com/davidmjackson/codecompaniondesktop> | Windows app |
| Code Companion Voice + Bridge | <https://github.com/davidmjackson/codecompanion> | VS Code extension pack |

**Install Desktop first, then Voice.** Desktop is the speech authority and must
be running before the VS Code side can pair with it.

To reinstall the VS Code side, follow `docs/INSTALL.md` in the
[Code Companion Voice repository](https://github.com/davidmjackson/codecompanion/blob/main/docs/INSTALL.md).

## 1. Prerequisites

- Windows
- .NET 8 SDK or later
- Inno Setup 6 (only needed to build the installer)
- Git

Confirm the .NET SDK from PowerShell:

```powershell
dotnet --info
```

The installer build script looks for `ISCC.exe` on PATH and in the default Inno
Setup install locations.

## 2. Clone

This repository is **private**, so authenticate first. With the GitHub CLI:

```powershell
gh repo clone davidmjackson/codecompaniondesktop D:\Development\CodeCompanionDesktop
```

The default branch is `main`, which always holds the current app.

Open this folder in a normal local Windows VS Code window, **not** a WSL Remote
window. The app targets `net8.0-windows` and needs the WindowsDesktop SDK on the
Windows side.

## 3. Verify the checkout builds

```powershell
dotnet test .\CodeCompanionDesktop.sln
```

Run this from Windows PowerShell. The test suite cannot run from WSL because the
app targets `net8.0-windows`.

If the build fails with a file lock, close any running Code Companion Desktop
instance first.

## 4. Build the installer

```powershell
.\scripts\build-installer.ps1 -AppVersion 0.1.0
```

Output:

```text
artifacts\installer\CodeCompanionDesktopSetup-0.1.0.exe
```

## 5. Install

1. Run `artifacts\installer\CodeCompanionDesktopSetup-0.1.0.exe`.
2. Launch Code Companion Desktop from the installer, Start Menu, or desktop
   shortcut.
3. Confirm the app opens with the Code Companion icon and the tray icon appears.

The installer is a **per-user** install. It installs to:

```text
%LOCALAPPDATA%\Programs\Code Companion Desktop
```

## 6. Restore configuration

Credentials and settings are **not** in this repository and are not restored by
reinstalling. Re-enter them:

1. Save your ElevenLabs API key in the desktop app. It is stored in **Windows
   Credential Manager**, not in the repo.
2. On the **Status** tab, confirm the app is running and the local bridge is
   healthy. A green readiness status means the bridge and provider key are
   configured; a red status lists the fix.

User settings live under `%APPDATA%\CodeCompanionDesktop`. Deleting that folder
resets the app to defaults.

## 7. Startup options

1. In the Startup section, enable `Start hidden to tray` to keep the app out of
   the way after launch.
2. Enable `Start with Windows sign-in` **from the installed app**.
3. Click `Refresh Diagnostics` and confirm the registered executable path points
   to `%LOCALAPPDATA%\Programs\Code Companion Desktop\CodeCompanionDesktop.exe`.

If `Start with Windows sign-in` was previously enabled from a debug build, turn
it off and back on from the installed app so the registered path updates.

## 8. Pair with VS Code

Now install the VS Code side — see
[the Voice repository's install guide](https://github.com/davidmjackson/codecompanion/blob/main/docs/INSTALL.md).

Once it is installed:

1. Open a project and use Codex or Claude Code.
2. On the first speech candidate, Desktop records the VS Code client as pending.
3. Approve it in the Desktop **Client Pairing** panel.
4. Confirm Desktop speaks the assistant's next final answer.

## Verify the whole chain

```powershell
.\scripts\send-speech-candidate.ps1 -Text "Reinstall verified."
```

If Desktop speaks that phrase, the app, provider key, and audio path all work —
independently of VS Code.

## If it does not speak

- **No speech at all** — confirm the Status tab is healthy and the VS Code
  client is approved in Client Pairing.
- **Provider errors** — reconfirm the ElevenLabs key in Speech Provider.
- The paired Desktop/Voice release checklist is in `docs/release-checklist.md`.
- Architecture and milestones are in `docs/architecture.md`.
