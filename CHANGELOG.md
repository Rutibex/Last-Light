# Changelog

## 0.9.0-rc.1 — 2026-10-15

First GitHub-ready release-candidate packaging of the supplied Lastlight build.

### Gameplay

- Deterministic science-fantasy colony simulation with autonomous settlers, equipment, injuries, relations and production.
- Multi-level engineering, climate, farming, animals, ruins and formula-based magic.
- Regional politics, real expeditions and trade, settlement legacy/institutions and a staged solar campaign with five optional finales.

### Reliability and interfaces

- Existing version 9 saves read earlier supported formats via explicit migrations.
- Additive validated JSON data packs and pack fingerprint checks.
- Optional non-serialized profiling, annual conservation checks, high-contrast UI and focused-button keyboard navigation.
- Solar scarcity applied to real regional food reserve updates.

### Distribution

- Split the Windows runtime from the source repository.
- Removed duplicated checkpoints, obsolete launcher scripts, generated verification logs and milestone notes; kept a compact compatibility-fixture subset.
- Added GitHub workflow files, player/developer documentation, screenshots, troubleshooting and third-party license notices.

**Outstanding:** no native Windows hands-on verification has been performed as part of this packaging pass. This remains a prerelease build rather than a stable 1.0 claim.
