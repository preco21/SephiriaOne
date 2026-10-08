# English / Korean translations

Added in `0.21.0`. SephiriaOne translates its settings panel, command help and
feedback, current status labels, addon costume-description lines and low-MP
notifications. Choose Korean (`ko`, the default since `0.27.1`) or English (`en`).
Existing saved selections are preserved. The game's own language setting is independent.

Since `0.37.2`, older or partial catalog files inherit missing entries from the
current DLL's bundled translations. This fixes new features appearing in English
after an upgrade even though Korean is selected. Existing custom values and files
are preserved; loading the updated addon is sufficient, with no catalog reset.

## Select a language

Use chat commands on the computer that has the addon installed:

```text
/one language ko
/one language en
/one language status
/one language reload
```

Selection is saved immediately. `status` prints the active language and exact
config path. `reload` reads edits to all three JSON files without restarting.
These are local presentation commands and do not require host authority.
They never apply a gameplay preset or modify session settings.

At addon startup, missing files and their directory are created under:

```text
Application.persistentDataPath/SephiriaOne/localization/
  config.json
  en.json
  ko.json
```

This is the game's user-data directory, alongside `SephiriaOne/session-preset.txt`,
not the Steam `AddOns` directory. Use `/one language status` to find it on your
machine. To select Korean through `config.json`:

```json
{
  "language": "ko"
}
```

Use exactly `en` or `ko`. Editing a file takes effect on `/one language reload`
or the next addon load; files are not polled during gameplay.

## Edit translations

Each catalog is a JSON object mapping the exact English source text to the text
to display. Edit values and preserve keys. For example, entries in `ko.json`:

```json
{
  "Stats": "능력치",
  "Fountain": "소원 분수",
  "Not enough MP to heal ({0}/{1} MP).": "회복에 필요한 MP가 부족합니다 ({0}/{1} MP)."
}
```

Add or edit these entries in the generated full catalog. A minimal catalog also
works: missing Korean entries use bundled Korean, then the English catalog and
its bundled defaults, then the original source. `{0}`, `{1}`, etc. are formatting
placeholders: keep all of them, but reorder them for natural phrasing. Preserve command examples, numeric syntax,
and the rich-text tags used in costume-description lines. Use standard JSON with
double quotes; comments, duplicate keys, non-string values and trailing commas
are rejected. UTF-8 is recommended.

Explicit blank translations and invalid placeholders keep their English/source
fallback for that entry and produce a warning in
`Player.log`. A malformed file causes the whole reload to fail while the last
good language and catalogs remain active. A startup failure uses embedded English
defaults. Files are limited to 1 MiB, 4096 entries and 16384 characters per key or
value; placeholder indices and alignment are bounded too.

Existing files are never overwritten automatically. Upgrading the addon keeps
custom translations; new missing entries use the current bundled translation.
Remove a custom key and reload to restore that entry's bundled wording. This
in-memory overlay never copies a previous custom value into the defaults and
never expands or rewrites existing files. To regenerate an unmodified full catalog,
back up and remove that one file, then restart the addon.
The source defaults are in `SephiriaOne/Localization/Catalogs/` and are embedded in
the addon DLL, so deploying the addon needs no additional translation files.

## Behavior and multiplayer

- An open panel refreshes its labels and Korean font on language change. Unapplied
  draft input is cleared; review values before applying another action.
- An open Wing-Eared Rabbit tooltip refreshes only its addon lines; native costume
  text stays intact. Switching language does not change potion options.
- Low-MP messages sent to unmodified guests already contain the host's selected
  language. No guest translation config, addon protocol or assets are required.
- Command names, stat IDs, canonical preset rows, player names and numeric parsing
  remain unchanged. Continue using `/stats luck x3`, for example, in either language.
- Previously emitted messages and captured fault details keep their original
  language. Technical exception details and protocol identifiers remain diagnostic
  text. Language changes do not replay state updates to rewrite history.

## Development and verification

`L.T` translates explicit source strings; `L.F` formats translated templates using
invariant culture. Dynamic values are supplied as arguments and never passed
through broad text replacement. Add new text to both catalogs when adding a UI
surface or message. Core synchronization decisions must use typed state, never
translated message equality. Static help strings are getters so they follow the
active language.

The game-provided `Newtonsoft.Json.dll` is referenced with `Private=false`; it is
not redistributed. Lookups use an in-memory snapshot. Reload publishes the whole
validated snapshot at once and triggers presentation refresh only. The panel
borrows the native Korean Galmuri font without changing native language, materials
or shared font fallback lists.

Verification includes strict loader/fallback/atomic-reload tests, bundled catalog
and placeholder coverage, English/Korean command parity, unchanged session intent,
live tooltip/name-status refresh, native alert payloads, existing gameplay suites
and installed-game UI contracts. Live rendering and multiplayer playtesting are
still required; this change was built with deployment disabled.

The 2026-10-09 audit found all 507 bundled keys translated, with only product names
and symbolic templates intentionally unchanged. The existing local Korean file
lacked 165 current keys; replacing the entire bundled snapshot with that older
file caused the English fallback. Upgrade regressions now exercise every bundled
key using a sparse custom catalog, preserve custom file bytes, and cover removal
of overrides, reload/restart, invalid-entry fallback and atomic failed reloads.
Lookup remains an in-memory dictionary read; merging occurs only on load/reload.
Verification passed: 50 loader tests, 2457 bundled catalog checks, the portable
and runtime integration suites, installed-game contracts, and Debug/Release
builds with deployment disabled. Live UI rendering remains untested.
