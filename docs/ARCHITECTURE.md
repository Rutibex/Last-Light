# Architecture

Lastlight separates a deterministic stateful simulation from a MonoGame DesktopGL client. The desktop owns presentation, input and GPU resources; the core owns world state, rules, commands, migration and persistence. The checks project exercises the core through executable scenarios and compatibility snapshots.

## Projects and dependencies

| Project | Role |
| --- | --- |
| `src/Lastlight.Core` | Definitions, world state, deterministic stepping, physical systems, characters, regional simulation, solar campaign, saves |
| `src/Lastlight.Desktop` | MonoGame client, renderer, panels, camera, audio, settings, native-interface checks |
| `src/Lastlight.Checks` | Console regression/acceptance harness, system edge cases, migration validation, long-simulation checks |

All projects target `net8.0`. `Directory.Build.props` enables nullable annotations, deterministic builds and warnings-as-errors. The desktop depends on `MonoGame.Framework.DesktopGL` 3.8.4.1. The check harness references the core without a graphics dependency.

## Simulation and command processing

`World` owns cells, entities, data-driven definitions and autonomous colony state. Commands are validated and applied in order; world state advances on a fixed simulation clock with reproducible randomness. Settlers navigate physically, claim resources, execute bills, respond to needs, suffer injuries and interact with other actors. Generation uses stable seeds; unique entity IDs are not reused. Game speed determines how many simulation ticks advance, not a different ruleset.

The physical model tracks water and contaminants, temperature, combustion/smoke, separate power and mechanical networks, plants and animal systems. Elevations, support and building obstructions determine accessibility. Production consumes real stock and transports work ingredients.

Regional play adds persistent factions, supply reserves, agreements, caravans, sites, travel and unique artifact custody. Institutions layer apprenticeships, study, adult bonds and a settlement succession archive on top of that shared world state. The solar campaign modifies real illumination, food production, climate and exposure; it does not maintain a separate cosmetic progress bar.

## Solar campaign and endings

`SolarCampaign`, `CampaignPolitics`, `CampaignDirector` and `CampaignProjects` handle stage boundaries, predictions, patron obligations, signed treaties, scenario pressure and construction of the five optional finales. Projects commit three material/work phases through existing logistics and path selection. They preserve spent-material ledgers across interruptions and saves. Completing one outcome closes the alternatives. Regional scarcity applies to the faction reserve ledger.

## Rendering, settings and interaction

`LastlightGame` hosts the MonoGame loop. `WorldRenderer` draws procedural world elements and the `Interface`/specialized panels expose orders and status. An interface settings file controls scaling, sound, input mapping, alerts and high contrast. Keyboard focus moves through visible enabled controls and rejects stale selection after panel changes; map and matrix interactions remain pointer-driven.

## Definitions and additive packs

The game loads `src/Lastlight.Desktop/Assets/definitions.json`. `ContentPacks.Load` optionally consumes `Assets/Mods/load-order.json` followed by ordered JSON files. Each pack is constrained by size, ID, schema, references and exact declared dependency versions. Base IDs cannot be overwritten. No dynamic assemblies or script interpreters are loaded. Built-in entity, recipe, formula and material pipelines consume validated merged data. Details: [Modding guide](MODDING.md).

## Persistence and compatibility

`SaveStore` writes versioned, checksummed worlds and view state, uses atomic writes and retains prior-file backups. The current file format is **9**, with migration paths for formats **1–8**. A canonical content fingerprint guards against silently loading saves under different semantics. Existing embedded settlement archives survive succession. The save directory comes from the host's local application data location, separate from the installation.

`World.Profile` is an optional JSON-ignored `SimulationProfile` that measures periodic subsystems without consuming world randomness or entering save snapshots.

## Test boundaries

The check harness includes deterministic continuations, scripted acceptance, UI smoke modes, package/mod diagnostics and long-run stress scenarios. This source repository contains a compact set of nine old-save snapshots needed by regression cases; it intentionally excludes generated logs and dozens of development checkpoints. For reproducible commands and limitations see [Testing](TESTING.md). Automated runs are not equivalent to native Windows QA or extensive player usability testing.
