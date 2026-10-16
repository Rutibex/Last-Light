# Tests and verification

The checks are executable C# console programs (not xUnit). Run them from the repository root after installing .NET 8 and restoring packages.

```sh
# Compile everything, including the game executable
dotnet build Lastlight.sln -c Release

# Foundation / interface and earlier colony acceptance checks
dotnet run --project src/Lastlight.Checks -c Release -- src/Lastlight.Desktop/Assets/definitions.json artifacts/checks

# Lightweight candidate-specific checks and fixture migrations
dotnet run --project src/Lastlight.Checks -c Release -- src/Lastlight.Desktop/Assets/definitions.json artifacts/release-checks --release-only

# Optional long-running 10-year stress path
dotnet run --project src/Lastlight.Checks -c Release -- src/Lastlight.Desktop/Assets/definitions.json artifacts/stress-checks --release-stress-only
```

**Important:** The release-only runner enumerates `.lastlight` snapshots in `tests/fixtures/`. This trimmed release has **nine** chosen fixtures, not the sixty-plus checkpoint set used during earlier internal development. It tests only the retained subset; do not interpret its resulting count as proof that every deleted checkpoint was rerun.

For a graphical check in an environment supporting SDL/OpenGL/OpenAL:

```sh
dotnet run --project src/Lastlight.Desktop -c Release -- --release-ui-checks
dotnet run --project src/Lastlight.Desktop -c Release -- --release-ui-checks --compact --scale 1.25
```

Native UI checks may require access to the host's graphics/audio/display. The release workflow builds on Windows but does **not** assert native GUI operation. The supplied Windows build must be manually launched, tested through save/load, resized, and exercised with mods before a final 1.0 quality claim.

## Test fixtures

The nine files in `tests/fixtures/` are historical saved-state compatibility inputs and long-run stress inputs. They are not user save slots or new game templates. Do not modify them casually: their value comes from preserving prior schema state. Output should go under ignored `artifacts/` paths.

## Smoke-test checklist for maintainers

- Fresh Windows extraction launches; new game displays assets and accepts input
- Pause/speed/zoom/menu, 1366×768 and 1600×900 with 1.25 UI scale
- Build and crafting bills, work claims, crop harvest and regional trade
- `F10`/`F11` save and reload, backup recovery after interrupted write
- Faction contact, campaign settings, flare warnings and at least one project
- Default mod load order (`[]`), example pack validation and save-fingerprint rejection
- Run on a machine with a real Windows x64 graphics driver, not just Linux CI

The checks listed above are **recommended release gates**, not claims they have all been performed for this package.
