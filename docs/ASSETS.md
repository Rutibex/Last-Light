# Game assets and attribution

The entire set of **eight files used as game runtime assets** is tracked in
[`src/Lastlight.Desktop/Assets/`](../src/Lastlight.Desktop/Assets/).
It is shipped with source clones, GitHub source downloads, and compiled player
packages. Nothing must be fetched from a separate asset host to build or run
the game. The Desktop project copies the complete directory to `Assets/`
beside the executable on build and publish.

## Inventory

| Asset | Function | SHA-256 |
| --- | --- | --- |
| [`Mods/example.json`](../src/Lastlight.Desktop/Assets/Mods/example.json) | Example optional content pack | `0e260454cf2d3d305bf11bf51a82d4ebbc33a84745121dbe9a700c63c344cb96` |
| [`Mods/load-order.json`](../src/Lastlight.Desktop/Assets/Mods/load-order.json) | Default mod-loading order | `37517e5f3dc66819f61f5a7bb8ace1921282415f10551d2defa5c3eb0985b570` |
| [`ambient.wav`](../src/Lastlight.Desktop/Assets/ambient.wav) | Atmospheric audio loop | `20737b91727829fcfad14bc9a7a59983114840783c8a8befeaee6ba979559b8f` |
| [`danger.wav`](../src/Lastlight.Desktop/Assets/danger.wav) | Warning / danger cue | `8270055c8c51619ad6b263647498355915cacce05d3808e38c53edfb15f0d26a` |
| [`definitions.json`](../src/Lastlight.Desktop/Assets/definitions.json) | Base world content, items, materials and rules data | `cd0ce037e3650e158469ae8dbe7a0710740670c99fb5ea44db992dda3b0d8e39` |
| [`discovery.wav`](../src/Lastlight.Desktop/Assets/discovery.wav) | Discovery cue | `310d120860aeef1adb3acf5d8c23276c7e71031ba3dd65ba9afc7e7425014f23` |
| [`font.png`](../src/Lastlight.Desktop/Assets/font.png) | Rasterized DejaVu Sans Mono glyph atlas (DejaVu license) | `423ad660504201fca1077fa9ec1e1d231423f7f1b789965a096155e74beda2bc` |
| [`formula.wav`](../src/Lastlight.Desktop/Assets/formula.wav) | Magic / formula cue | `adc432e15d8bba7f170e4fde31bca361792cb51c3cdc7d676ad4e18f17ab5ff6` |

## What is and is not a separate asset

- The world, terrain, structures, characters, and objects are rendered primarily
  from procedural geometry in the C# renderer. There is no missing external
  sprite sheet, character-animation pack, tile atlas, or asset download.
- Six selected in-game screenshots live in [`docs/screenshots/`](screenshots/)
  for the README and presentation only; they are not loaded during gameplay.
- Generated binaries, .NET runtime libraries, developer test images, game saves,
  and intermediate build outputs are **not** source assets and remain out of
  the source archive. The player ZIP provides the actual compiled runtime.

## Licensing

Original Lastlight code, written game data, original sounds and project
documentation are released under the repository [MIT License](../LICENSE).
The `font.png` glyph atlas uses glyphs derived from **DejaVu Sans Mono**:
the font authors’ license, reproduced in
[`licenses/DejaVu.txt`](../licenses/DejaVu.txt), continues to apply to
those glyphs. The rest of the third-party software and licensing credits
are recorded in [`licenses/THIRD-PARTY.md`](../licenses/THIRD-PARTY.md).
The MIT license does not override third-party licenses.

## Adding new art and audio

Place new game assets under `src/Lastlight.Desktop/Assets/` and keep their
original filenames and directory paths consistent with loading code.
Update this inventory and record any external creators, source links, and
required notices in the third-party license documents before release.
