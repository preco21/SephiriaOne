# GitHub release updates

Available since `0.38.0`. The addon checks the public
[SephiriaOne releases](https://github.com/preco21/SephiriaOne/releases) and announces
a newer stable version in local chat and the settings panel. Checks do not
download or install an update. No GitHub account, hotkey or guest addon is needed.

## Update through chat or the panel

| Action | Command |
| --- | --- |
| Check now | `/one update check` |
| Show running/installed version and progress | `/one update status` |
| Show update command help | `/one update help` |
| Download, verify and install the offered version | `/one update install` |
| Enable automatic startup checks | `/one update auto on` |
| Disable automatic startup checks | `/one update auto off` |

In the host's existing SephiriaOne panel (`/one ui` or the pause-menu button),
choose **Updates** at the top, then **Update**. The same view has **Check for
updates**, automatic-check On/Off, and **Open releases**. The header shows an
available-update indicator on other pages too. The Update button and install
command use the same service. Commands work locally even without host authority;
the existing gameplay panel remains host-only.

Keep the game open while downloading/validating/installing. Wait for the message
that the version is installed, then fully close and restart Sephiria. The game
continues using the previous assembly until that restart. Returning to the lobby
or title screen does not activate new code. An interrupted download does not
schedule hidden work after exit: issue the install action again next time.

Messages and controls support English and Korean. Startup availability notices
wait until the local chat log exists, and are deduplicated within that controller
lifetime. They are not broadcast to other players.

## Local policy and failures

`Application.persistentDataPath/SephiriaOne/updates.json` stores automatic-check
preference, last check, server cooldown and the cached offer. Automatic startup
checking defaults to on, with a six-hour persisted interval. Manual checks have a
30-second local cooldown; GitHub rate limits take precedence and survive restart.
Automatic-check toggles persist for subsequent launches. A busy operation must
finish before another action or preference change can start.

Release discovery uses one asynchronous operation and no per-player work.
Downloading, hashing, ZIP validation and installation run off the Unity thread.
The local controller checks completion every 0.25 seconds. UI status is refreshed
through the existing panel; gameplay reconciliation/presets are independent.

Equal/older versions and prereleases are never installed. Before downloading, the
updater rechecks that the offered version, URL, size and digest still match the
selected release. If the release changed, use Check again and review the new
offer. A network, validation, permission or concurrent-file error is reported in
`Player.log`; the panel provides a manual release link. Linked/reparse installation
folders are unsupported by this first implementation. Keep a single active copy
of the addon under AddOns; an updater instance manages only its own folder.

Existing saved presets and translation files are not part of release packages
and remain untouched. The updater never modifies other addon folders or game DLLs.
Game-build compatibility still relies on the existing native contract guards;
publishing a release does not establish compatibility with all future game builds.

## Installation and rollback

The supported release ZIP contains exactly two files at its root:

```text
SephiriaOne.dll
metadata.json
```

The updater verifies the GitHub asset SHA-256, bounded archive/expanded sizes,
exact entry names, addon identity, and agreement between tag, metadata and DLL
version. Downloaded assemblies are inspected as metadata, never executed for
validation. GitHub is the trusted distribution source; its same-source digest
does not constitute an independent author signature.

It writes a new `SephiriaOne.X.Y.Z.dll` beside the running DLL and changes only
`metadata.json` atomically to select that completed file. It retains the previous
DLL and creates `.one-update-<id>.previous.json` containing the old metadata.
Temporary staging files are removed. Completed version DLLs are conservatively
retained even after a failed commit, because another process may have selected
them. They are never overwritten. This also means a leftover destination may
require manual review before retrying the same version.

To roll back, **close the game**, back up the current `metadata.json`, and copy
the appropriate `.one-update-<id>.previous.json` over it. Check that the DLL named
in that backup is still present, then launch again. Another option is manually
reinstalling the desired official ZIP into the same addon folder. Do not create
backup addon directories inside `AddOns`, because the game scans each child as
an active addon. There is no automatic startup-health rollback if a new DLL cannot
initialize at all.

## Preparing releases

The first updater-capable version requires manual installation. Publishing later
stable releases supplies updates; committing or pushing source alone does not.
Use matching `vX.Y.Z` GitHub tag, project version and metadata version, then:

```powershell
dotnet build SephiriaOne/SephiriaOne.csproj -c Release -p:DeployMod=false
./scripts/Package-Mod.ps1
```

The packaging script verifies DLL/metadata identity and version and writes
`artifacts/SephiriaOne.zip`, printing its SHA-256. It does not build, deploy or
publish. Attach that ZIP to a stable GitHub release in `preco21/SephiriaOne`.
The public asset must expose GitHub's `sha256:` digest. Do not add source,
dependencies, PDBs, user settings or game assemblies to that ZIP. Harmony and
translations are already embedded in the DLL.

## Verification and remaining live checks

Automated coverage includes release/version validation, HTTP errors/limits and
redirects, cache behavior, explicit-only installs, duplicate commands, cancelled
and disposed controllers, local commands without host authority, ZIP/path/hash
rejection, locked metadata, concurrent manual selection and atomic installation
into temporary directories. The native inspection suite checks metadata filename
selection, command/button wiring, local message use and gameplay isolation.

The client also successfully read the real public `v0.33.0` release without
installing it. The new package is tested against a temporary old-release fixture.
Actual Unity TLS, live chat/panel rendering and activation after restarting the
game still require a live smoke test. This task does not deploy the addon or
publish a release.
