# GitHub Releases update investigation

Investigated 2026-10-09 against the installed game loader, current addon source,
and the public GitHub Releases API. This is a feasibility report and proposed
architecture, not an implemented or approved updater specification.

## Conclusion

Automatic updates are feasible without changes on unmodified multiplayer guests.
The updater would manage only the local addon installation. A full game restart
must activate the new code; no live assembly replacement or session re-sync is
needed. The inspected SDK has no built-in update service.

The recommended installation mechanism is a version-specific DLL plus an atomic
metadata switch. This uses the existing SDK loader directly and avoids replacing
a loaded DLL or requiring an external installer. Update preferences are local
installation settings and must be separate from session presets.

## Requested interaction

The user selected automatic discovery with explicit installation:

- Check for a newer release automatically; announce availability in local chat
  and the addon UI. Do not automatically install merely because a check succeeds.
- Let the player choose Update in the UI or execute `/one update install`.
  `/one update check` and `/one update status` expose discovery and progress.
- With the proposed version-specific DLL transaction, installation can happen
  while the game is running, but the new code activates only after a full restart.
  Report this distinction, including the currently running and pending versions.
- If implementation instead needs an after-exit installer, explicitly tell the
  player that the update is queued for after exit. That variant needs a separate
  helper; code inside the game cannot continue downloading after its process ends.
  It must never report success merely because a helper was scheduled.

Suggested localized messages include "A newer SephiriaOne version is available:
{0}. Use /one update install or the Update button", download/validation progress,
and "Version {0} is installed. Restart Sephiria to use it". Announcements are
local to the installation, not messages broadcast to unmodified guests. Queue a
startup discovery notice until the local chat UI is available, deduplicate it
per discovered version, and keep an available-update indicator in the panel.
No new hotkey is required.

## Verified evidence

- The public repository is `preco21/SephiriaOne`. Public release queries and
  downloads succeeded without authentication.
- The latest published stable release at inspection was `v0.33.0`, while the
  working addon was `0.37.2`. A release updater must never automatically downgrade
  a newer local build merely because GitHub marks an older release as latest.
- The uploaded asset is `SephiriaOne.zip`, 1,006,651 bytes. Its ZIP root contains
  only `metadata.json` and `SephiriaOne.dll`; it has no enclosing addon directory.
  The DLL is 2,964,992 bytes; metadata declares `0.33.0` and `SephiriaOne.Entry`.
- The downloaded archive SHA-256 matched the API asset digest:
  `ccab6f149e2c04a12ebae98bd61470021cca249e7717c005ca6609cf46240af4`.
  It was inspected in temporary storage, not installed or executed.
- There is no tracked `.github` release workflow or packaging script. The only
  tracked deployment script is `scripts/Deploy-Mod.ps1`, which copies the fixed
  DLL and metadata into `AddOns/SephiriaOne`.
- Installed `AddOnLoader` scans immediate child directories of `AddOns`. It
  reads each `metadata.json`, resolves its explicit `dllFile`, and calls
  `Assembly.LoadFrom`. Multiple DLLs are not loaded when `dllFile` is specified.
  Therefore a filename such as `SephiriaOne.0.38.0.dll` is supported by the loader.
- `UnloadAll` invokes each mod's cleanup callback and clears its list; it does
  not unload assemblies. `HorayModBase.UnloadMod` only invokes `OnModUnloaded`.
  `GameDataLoader.Awake` loads addons before initializing the databases.
- The loader records `LoadedAddOn.FolderPath` only after `OnModLoaded` returns.
  Early updater initialization must not assume its entry already appears in
  `LoadedMods`. The executing assembly's location can identify the installation;
  validate it against `AddOnLoader.AddOnsPath` rather than a hardcoded Steam path.
- Both `System.Net.Http.dll` and Unity's web-request module are installed. The
  actual network implementation still needs an in-game TLS/request smoke test.
- Presets and translations already live beneath
  `Application.persistentDataPath/SephiriaOne`, outside the addon install folder.
- The current settings panel is host-only and requires an active game UI.
  Updater checks/settings must not depend on that panel, `NetworkServer.active`,
  player membership, or the gameplay reconciliation loop.

## Approaches

| Approach | Benefits | Cost or limitation |
| --- | --- | --- |
| Version-specific DLL and atomic metadata switch (recommended) | Uses existing loader; running assembly stays untouched; one metadata file commits the installation; no helper process | Requires updater-owned filenames, backup tracking and cleanup; new code activates only on full restart |
| Fixed DLL replaced by a helper after game exit | Keeps the current two-file installation names | Requires shipping/maintaining a helper, exit detection, handling rapid relaunches, and recovery from partial two-file replacement |
| Automatic check and release link only | Smallest change; installation remains manual | Does not provide automatic installation |

A separate bootstrap can later support recovery when a new addon cannot load at
all, but adds a second maintained binary and changes the entry/loading structure.
It is unnecessary for basic release discovery and restart-based installation.

## Proposed installation transaction

1. Compare the running assembly's version with a validated published stable
   release. Ignore drafts, prereleases, equal versions and older versions.
2. Select the named binary ZIP asset, never GitHub's generated source archive.
   Download to bounded temporary storage outside `AddOns`, with cancellation,
   timeout, HTTPS and asset size/digest verification. Missing verification data
   leaves the current installation active and offers the release link.
3. Validate archive entry paths, entry count and expanded size. Extract only the
   declared package files; reject traversal, absolute paths, duplicates and
   unexpected executable content. Check metadata identity, entry class and
   version against the release, and inspect DLL metadata without loading it into
   the game process. A same-source hash checks integrity, not independent author
   authentication; the configured GitHub repository remains the trust boundary.
4. Write the DLL under a new, immutable, updater-owned version-specific filename
   inside the actual addon folder. Fully write and verify it before activation.
   Never overwrite an existing different file at that version's destination.
5. Prepare complete metadata pointing `dllFile` to that filename. Preserve the
   previous installation record and atomically replace the live metadata on the
   same volume. This single-file commit is the activation boundary: a launch
   observing old metadata has the old DLL available; one observing new metadata
   has the fully written new DLL available.
6. Continue running the old assembly until the player closes and restarts the
   game. Show both running and installed/pending versions. Do not call
   `LoadAll`, unload Harmony hooks, disconnect peers or restart the game for them.
7. Keep the previous DLL available for rollback. Backups/staging must not become
   sibling addon directories: the game would discover them as additional mods.
   Cleanup may remove only files recorded as updater-owned, and must retain
   every currently referenced/running DLL plus the previous version.

Interrupted downloads or a crash before the metadata commit leave the old
installation selected. A crash after the commit selects the fully staged new
DLL on the next launch. Atomic replacement, filesystem permissions, antivirus
interference and crash recovery still require real filesystem and game tests.
An access-denied result must preserve the current installation and explain manual
installation; no silent elevation or permission changes.

This transaction also avoids requiring a reliable `OnModUnloaded`/quit callback.
It cannot automatically recover if the new addon fails before its own updater
initializes. Keep a documented manual metadata rollback procedure; guaranteed
automatic startup-health rollback would require an independent bootstrap/helper.

## Integration and performance

Use a small local `Updates` module, separate from `SessionSettings`:

- A release client handles HTTP, validated response DTOs, version comparison and
  stable asset selection.
- An installer handles archive validation, local paths and the metadata
  transaction. It does not touch players, saves or gameplay state.
- A coordinator owns the local policy, one in-flight operation, cancellation,
  revisioned status and startup scheduling.
- Existing command/UI adapters display that status through `L.T`/`L.F`, with
  complete English and Korean catalog entries. Commands such as
  `/one update check|status` and explicit install/rollback actions can share the
  coordinator. A panel section can expose the same operations without granting
  guests access to host gameplay settings or adding a hotkey.

Automatic discovery is requested; download and installation require an explicit
Update action. Offer a local preference to disable automatic checks. A suggested
schedule is once after startup, subject to a persisted six-hour check interval,
plus an explicit check action.
Keep all networking and archive/hash work off the Unity frame thread; marshal
only status publication back to the main thread and reject completions from a
disposed controller generation. No per-player or per-frame HTTP/filesystem work.
Cancel unfinished work on disposal without relying on disposal to finish an
installation. Disabling automatic checks does not revoke a separately requested
installation; an already committed installation remains clearly reported as
pending until restart.

Use a User-Agent, cached ETag/conditional requests, bounded retries and GitHub's
rate-limit/Retry-After headers. Unauthenticated requests have a shared per-IP
limit of 60/hour; unauthenticated 304 responses must not be assumed exempt.
Offline/rate-limited responses should leave gameplay and the installed version
unchanged. Do not embed a maintainer token or require a player GitHub account.

## Release preparation and required verification

The first updater-capable release still needs manual installation. Subsequent
updates consume published release assets; ordinary commits/pushes do not publish
an update. Standardize the ZIP asset name/layout and require consistent release,
DLL and metadata versions. Add a packaging check using `DeployMod=false` and
exclude game assemblies and user settings. GitHub immutable releases are an
optional additional protection for published assets/tags.

The existing game-contract checks should continue to reject incompatible native
hooks after an update. The current release format declares no supported game
build range, so automatic installation cannot claim compatibility with every
future game update. A tested game-build declaration/update manifest is a separate
release-policy decision if pre-install compatibility blocking is required.

Before implementation is considered complete, test:

- Version ordering, older latest release, prerelease/missing asset and mismatched
  tag/DLL/metadata; missing/bad hash; malformed, oversized and hostile archives.
- Offline/timeouts/rate limits/304, duplicate clicks, setting changes during a
  request, disposal/reload, cache corruption and stale async completions.
- Custom Steam paths, permissions, locked files, interrupted writes, interruption
  before/after metadata commit, rapid relaunch, duplicate addon installations,
  cleanup ownership, rollback and local development builds.
- Existing presets/custom translations byte-for-byte, host/guest independence,
  no gameplay mutation or new peer protocol, English/Korean status UI.
- Actual Unity network behavior and the SDK loading a version-named DLL after a
  full restart; no mid-session hot reload and no frame-time stalls.

No runtime code was changed, no updater was executed, and no files in the game
installation or user settings were modified during this investigation.

## References

- [GitHub latest release API](https://docs.github.com/en/rest/releases/releases#get-the-latest-release)
- [GitHub release asset API](https://docs.github.com/en/rest/releases/assets#get-a-release-asset)
- [GitHub API best practices](https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api)
- [GitHub API rate limits](https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api)
- [GitHub immutable releases](https://docs.github.com/en/code-security/concepts/supply-chain-security/immutable-releases)
- [Inspected published release](https://github.com/preco21/SephiriaOne/releases/tag/v0.33.0)
- [Existing loader findings](development-notes.md#loader-and-api-findings)
- [Current localization architecture](localization.md)
