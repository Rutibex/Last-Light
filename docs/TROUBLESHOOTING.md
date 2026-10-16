# Troubleshooting

**Game will not start:** Extract the *entire* Windows ZIP first; do not launch `Lastlight.exe` from a ZIP viewer. Keep `.dll` files, native libraries and `Assets/` beside it. Run `Run-Lastlight.bat` to keep its error message visible. Update your GPU driver if OpenGL initialization fails.

**Missing `Assets/definitions.json`:** The distribution was separated or extracted incompletely. Redownload or publish the entire directory, not the `.exe` alone.

**Saves or preferences:** On Windows, open `%LOCALAPPDATA%\Lastlight\Saves`. The client attempts to write `startup-error.txt` there if startup fails. Back up this directory before removing preferences or experimenting with content packs.

**A save refuses to load after changing mods:** The game's content fingerprint intentionally protects incompatible saves. Restore the identical definitions and enabled packs, including changed recipes or entities. Do not hand-edit the checksum.

**Game is paused on launch:** Expected. Press `Space`; `1`, `2`, `3` choose speed.

**Nothing gets built:** Verify accessible materials, a reachable worksite, available workers, work priorities, schedules, restrictions and active claims. Construction requires resource hauling rather than instant placement.

**Workers do not get food or warmth:** Check growing light, supplies, storage permissions, beds, heating and connected networks. Fire/water/environmental damage can disable production.

**Controls hard to reach:** Use Menu to select UI scale 1.00/1.10/1.25 or High contrast. `Ctrl+Up/Down` changes focus between some visible buttons; `Enter` activates. Map and matrix interactions still require a mouse.

**Build fails:** Install the full .NET **8 SDK**, not only the runtime, and ensure NuGet restore can reach MonoGame packages. See [Building](BUILDING.md).

When filing an issue, include Windows version, GPU/driver, steps to reproduce, the error message, a screenshot if useful, and whether mods are enabled. Never post a private save with personal details without reviewing it first.
